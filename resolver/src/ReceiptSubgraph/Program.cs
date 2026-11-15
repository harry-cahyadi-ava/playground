using ReceiptSubgraph;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReceiptSubgraph(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

// GraphQL endpoint (default route: /graphql). The schema and request executor are built eagerly at startup.
app.MapGraphQL();
app.MapHealthChecks("/health");

app.Run();

// Exposes the entry point to integration tests.
public partial class Program;
