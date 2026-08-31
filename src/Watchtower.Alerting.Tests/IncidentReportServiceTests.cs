using QuestPDF.Infrastructure;
using Watchtower.Alerting.Reporting;
using Watchtower.Entities.Alerts;
using Watchtower.Entities.Enums;

namespace Watchtower.Alerting.Tests;

public class IncidentReportServiceTests
{
    static IncidentReportServiceTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly DateTimeOffset From = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 8, 31, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public void BuildIncidentReport_WithAlerts_ReturnsValidPdfBytes()
    {
        var alerts = new List<Alert>
        {
            new()
            {
                CreatedAt = From.AddDays(1),
                Severity = AlertSeverity.High,
                DetectorName = "brute_force",
                Title = "Brute-force: 6 failed logins from 77.240.1.10",
                Explanation = "6 failed logins from 77.240.1.10 within 3 min.",
                MitreTechniques = ["T1110"],
                RelatedEventIds = [Guid.NewGuid()],
            },
        };

        var pdf = new IncidentReportService().BuildIncidentReport(alerts, From, To);

        Assert.NotEmpty(pdf);
        Assert.Equal("%PDF"u8.ToArray(), pdf.Take(4).ToArray());
    }

    [Fact]
    public void BuildIncidentReport_NoAlerts_StillReturnsValidPdfBytes()
    {
        var pdf = new IncidentReportService().BuildIncidentReport([], From, To);

        Assert.NotEmpty(pdf);
        Assert.Equal("%PDF"u8.ToArray(), pdf.Take(4).ToArray());
    }
}
