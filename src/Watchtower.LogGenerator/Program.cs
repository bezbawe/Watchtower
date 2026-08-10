using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Watchtower.LogGenerator;

// Конфиг: appsettings.json + переменные окружения + CLI (--Generator:PeriodHours=48 и т.п.).
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var options = config.GetSection(GeneratorOptions.SectionName).Get<GeneratorOptions>() ?? new GeneratorOptions();

var now = DateTimeOffset.UtcNow;
var events = SyntheticLogStream.Generate(options, now);

var byScenario = events
    .Where(e => e.Fields.TryGetValue("scenario", out _))
    .GroupBy(e => e.Fields["scenario"])
    .ToDictionary(g => g.Key, g => g.Count());
var anomalyCount = byScenario.Values.Sum();

Console.WriteLine($"Watchtower log generator (seed={options.Seed})");
Console.WriteLine($"  period            : last {options.PeriodHours}h ({now.AddHours(-options.PeriodHours):u} .. {now:u})");
Console.WriteLine($"  total events      : {events.Count}");
Console.WriteLine($"  normal background : {events.Count - anomalyCount}");
Console.WriteLine($"  anomalies         : {anomalyCount} ({(double)anomalyCount / Math.Max(1, events.Count):P1})");
foreach (var (scenario, count) in byScenario.OrderBy(kv => kv.Key))
    Console.WriteLine($"    - {scenario,-12}: {count}");
Console.WriteLine($"  target API        : {options.ApiBaseUrl}");

var webOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
using var http = new HttpClient { BaseAddress = new Uri(options.ApiBaseUrl) };

var accepted = 0;
var batchNo = 0;
foreach (var batch in events.Chunk(options.SendBatchSize))
{
    batchNo++;
    var payload = JsonSerializer.Serialize(batch, webOptions);
    using var content = new StringContent(payload, Encoding.UTF8, "application/json");

    HttpResponseMessage response;
    try
    {
        response = await http.PostAsync("/api/events", content);
    }
    catch (HttpRequestException ex)
    {
        Console.Error.WriteLine($"Failed to reach API at {options.ApiBaseUrl}: {ex.Message}");
        Console.Error.WriteLine("Is Watchtower.Web running? (dotnet run --project src/Watchtower.Web --launch-profile http)");
        return 1;
    }

    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync();
        Console.Error.WriteLine($"Batch {batchNo}: API returned {(int)response.StatusCode}: {body}");
        return 1;
    }

    var responseBody = await response.Content.ReadAsStringAsync();
    using var doc = JsonDocument.Parse(responseBody);
    accepted += doc.RootElement.GetProperty("accepted").GetInt32();
}

Console.WriteLine($"Sent {batchNo} batch(es); API accepted {accepted}/{events.Count} events.");
return 0;
