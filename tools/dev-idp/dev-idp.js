// Minimal local identity provider for testing the receipt subgraph. Development use only; no dependencies.
//
// Start:  node dev-idp.js [port] [issuer] [audience]   (or `npm start` in this folder)
//   defaults: 5099, http://localhost:<port>, receipts
//   The authority (where the subgraph finds the keys) is always http://localhost:<port>. The issuer is the name that
//   the discovery document and the tokens carry in "iss"; set it to make tokens look like your real provider's.
//   The audience is the default "aud" of issued tokens; match it to GraphQL:Authorization:Audience.
//
// Endpoints:
//   POST /token                            returns a signed access token as JSON. The optional JSON body sets the claims:
//       { "sub": "customer-1",             subject, becomes the customer id (default customer-1; null or "" leaves it out)
//         "roles": ["receipts.read"],      roles (default none)
//         "aud": "receipts",               audience (default: the audience above)
//         "issuer": "http://localhost:5099",  iss claim; default is the issuer above. The subgraph rejects any other value.
//         "expiresIn": 3600 }              lifetime in seconds (default 28800; negative gives an expired token)
//   GET /.well-known/openid-configuration  discovery document
//   GET /jwks                              signing keys
//
// A new key is generated on every start, so restart the subgraph after restarting this app.
//
// Run the subgraph against it (Issuer is only needed when you started this app with a custom issuer):
//   $env:GraphQL__Authorization__Authority='http://localhost:5099'
//   $env:GraphQL__Authorization__Issuer='<the issuer you passed>'
//   $env:GraphQL__Authorization__Audience='receipts'
//   $env:GraphQL__Authorization__RequireHttpsMetadata='false'
//   dotnet run --project resolver/src/ReceiptSubgraph/ReceiptSubgraph.csproj
//
// Get a token and call the subgraph:
//   $t = (Invoke-RestMethod http://localhost:5099/token -Method Post -ContentType application/json `
//     -Body '{"sub":"customer-1"}').access_token
//   Invoke-RestMethod <subgraph url>/graphql -Method Post -Headers @{ Authorization = "Bearer $t" } `
//     -ContentType application/json -Body '{"query":"{ receipts { __typename } }"}'
const crypto = require('crypto');
const http = require('http');

const port = Number(process.argv[2] || 5099);
const authority = `http://localhost:${port}`;
const issuer = process.argv[3] || authority;
const audience = process.argv[4] || 'receipts';
const kid = crypto.randomBytes(8).toString('hex');

const { publicKey, privateKey } = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
const jwk = { ...publicKey.export({ format: 'jwk' }), kid, use: 'sig', alg: 'RS256' };

const b64url = (value) => Buffer.from(value).toString('base64url');

function createToken({ sub, roles, aud, iss, expiresIn }) {
  const now = Math.floor(Date.now() / 1000);
  const header = b64url(JSON.stringify({ alg: 'RS256', typ: 'JWT', kid }));
  const claims = { iss, aud, roles, iat: now, nbf: Math.min(now, now + expiresIn - 60), exp: now + expiresIn };
  if (sub) claims.sub = sub;
  const payload = b64url(JSON.stringify(claims));
  const signature = crypto.sign('sha256', Buffer.from(`${header}.${payload}`), privateKey).toString('base64url');
  return `${header}.${payload}.${signature}`;
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    req.on('data', (chunk) => chunks.push(chunk));
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
    req.on('error', reject);
  });
}

http.createServer(async (req, res) => {
  const url = new URL(req.url, authority);
  const send = (body, status = 200) => {
    res.statusCode = status;
    res.setHeader('Content-Type', 'application/json');
    res.end(JSON.stringify(body));
  };

  if (url.pathname === '/token') {
    if (req.method !== 'POST') {
      res.setHeader('Allow', 'POST');
      return send({ error: 'use POST with an optional JSON body' }, 405);
    }

    const text = (await readBody(req)).trim();
    let body;
    try {
      body = text ? JSON.parse(text) : {};
    } catch {
      return send({ error: 'the request body must be valid JSON' }, 400);
    }
    if (body === null || typeof body !== 'object' || Array.isArray(body)) {
      return send({ error: 'the request body must be a JSON object' }, 400);
    }

    const expiresIn = body.expiresIn ?? 8 * 3600;
    if (typeof expiresIn !== 'number' || !Number.isFinite(expiresIn)) {
      return send({ error: 'expiresIn must be a number of seconds' }, 400);
    }
    const roles = body.roles ?? [];
    if (!Array.isArray(roles) || roles.some((role) => typeof role !== 'string')) {
      return send({ error: 'roles must be an array of strings' }, 400);
    }

    const token = createToken({
      sub: 'sub' in body ? body.sub : 'customer-1',
      roles,
      aud: body.aud || audience,
      iss: body.issuer || body.authority || issuer,
      expiresIn,
    });
    send({ access_token: token, token_type: 'Bearer', expires_in: expiresIn });
  } else if (url.pathname === '/.well-known/openid-configuration') {
    send({ issuer, jwks_uri: `${authority}/jwks`, token_endpoint: `${authority}/token`, id_token_signing_alg_values_supported: ['RS256'] });
  } else if (url.pathname === '/jwks') {
    send({ keys: [jwk] });
  } else {
    send({ error: 'not found' }, 404);
  }
}).listen(port, () => {
  console.log(`Dev identity provider listening on ${authority} (issuer: ${issuer})`);
  console.log(`Get a token: POST ${authority}/token with an optional JSON body, e.g. {"sub":"customer-1"}`);
});
