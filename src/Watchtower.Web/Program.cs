using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Watchtower.Alerting;
using Watchtower.Detection;
using Watchtower.Ingestion;
using Watchtower.Ingestion.Buffering;
using Watchtower.Ingestion.Dtos;
using Watchtower.Ingestion.Normalization;
using Watchtower.Ingestion.Parsing;
using Watchtower.Repository;
using Watchtower.Web.Alerting;
using Watchtower.Web.Components;
using Watchtower.Web.Hubs;
using Watchtower.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server + SignalR (live-обновление дашборда).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();

// Единый хост Watchtower: приём логов + детекция + алертинг + дашборд (§4: Api+Web схлопнуты).
builder.Services.AddWatchtowerDbContext(
    builder.Configuration.GetConnectionString("Watchtower")
    ?? throw new InvalidOperationException("Connection string 'Watchtower' is not configured."));
builder.Services.AddWatchtowerRepositories();
builder.Services.AddWatchtowerIngestion(builder.Configuration);
builder.Services.AddWatchtowerDetection(builder.Configuration);
builder.Services.AddWatchtowerAlerting(builder.Configuration);

// Live-канал алертов (SignalR) — host-реализация IAlertBroadcaster.
builder.Services.AddSingleton<IAlertBroadcaster, SignalRAlertBroadcaster>();
builder.Services.AddScoped<DashboardService>();

var app = builder.Build();

// В dev применяем миграции автоматически, чтобы `dotnet run` был самодостаточным.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WatchtowerDbContext>();
    await db.Database.MigrateAsync();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<AlertsHub>("/alertsHub");

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

// Приём структурированных событий: одиночный объект ИЛИ массив (батч). (Перенесено из Watchtower.Api.)
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

app.Run();
