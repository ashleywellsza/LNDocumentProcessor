using LNDocumentProcessor.Api;
using LNDocumentProcessor.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDocumentProcessing(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDocumentEndpoints();

app.Run();

// Exposed so the WebApplicationFactory-based tests can reference the entry point.
public partial class Program;
