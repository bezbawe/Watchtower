using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Watchtower.Detection.Mitre;
using Watchtower.Entities.Alerts;

namespace Watchtower.Alerting.Reporting;

// PDF-отчёт по инцидентам за период (QuestPDF) — интеграция host-уровня (§4), но сам билдер
// чистая логика: список алертов + период на входе, PDF-байты на выходе.
public class IncidentReportService
{
    public byte[] BuildIncidentReport(IReadOnlyList<Alert> alerts, DateTimeOffset from, DateTimeOffset to)
    {
        var ordered = alerts.OrderByDescending(a => a.CreatedAt).ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("Watchtower — Incident Report").FontSize(18).Bold();
                    col.Item().Text($"Period: {from:yyyy-MM-dd HH:mm} – {to:yyyy-MM-dd HH:mm} UTC")
                        .FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"Total incidents: {ordered.Count}").Bold();
                });

                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Spacing(10);

                    if (ordered.Count == 0)
                    {
                        col.Item().Text("No incidents in this period.").Italic();
                        return;
                    }

                    foreach (var alert in ordered)
                    {
                        col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(8).Column(item =>
                        {
                            item.Item().Row(row =>
                            {
                                row.RelativeItem().Text(alert.Title).Bold();
                                row.ConstantItem(80).AlignRight().Text(alert.Severity.ToString());
                            });
                            item.Item().Text($"{alert.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC · {alert.DetectorName}")
                                .FontColor(Colors.Grey.Darken1);
                            item.Item().Text(alert.Explanation);
                            if (alert.MitreTechniques.Count > 0)
                                item.Item().Text($"MITRE ATT&CK: {string.Join(", ", alert.MitreTechniques.Select(MitreAttackCatalog.Describe))}").Italic();
                        });
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}
