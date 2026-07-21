using System.Text.Json;
using Watchtower.Ingestion;
using Watchtower.Ingestion.Buffering;
using Watchtower.Ingestion.Dtos;
using Watchtower.Ingestion.Normalization;
using Watchtower.Ingestion.Parsing;
using Watchtower.Repository;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddWatchtowerDbContext(
    builder.Configuration.GetConnectionString("Watchtower")
    ?? throw new InvalidOperationException("Connection string 'Watchtower' is not configured."));
builder.Services.AddWatchtowerRepositories();
builder.Services.AddWatchtowerIngestion(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

// Приём структурированных событий: одиночный объект ИЛИ массив (батч).
app.MapPost("/api/events", async (
    JsonElement body,
    ILogEventNormalizer normalizer,
    IEventIngestQueue queue,
    CancellationToken cancellationToken) =>
{
    List<LogEventDto?>? dtos;
    try
    {
        dtos = body.ValueKind == JsonValueKind.Array
            ? body.Deserialize<List<LogEventDto?>>(jsonOptions)
            : [body.Deserialize<LogEventDto>(jsonOptions)];
    }
    catch (JsonException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    var accepted = 0;
    foreach (var dto in dtos ?? [])
    {
        if (dto is null)
            continue;
        await queue.EnqueueAsync(normalizer.Normalize(dto), cancellationToken);
        accepted++;
    }

    return Results.Accepted(value: new { accepted });
});

// Приём простого текстового формата (logfmt): одна строка = одно событие.
app.MapPost("/api/events/text", async (
    HttpRequest request,
    ITextLogParser parser,
    ILogEventNormalizer normalizer,
    IEventIngestQueue queue,
    CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(request.Body);
    var text = await reader.ReadToEndAsync(cancellationToken);

    var accepted = 0;
    foreach (var dto in parser.Parse(text))
    {
        await queue.EnqueueAsync(normalizer.Normalize(dto), cancellationToken);
        accepted++;
    }

    return Results.Accepted(value: new { accepted });
})
.Accepts<string>("text/plain");

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
