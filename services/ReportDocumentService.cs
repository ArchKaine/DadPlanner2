using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DadPlanner2.Models;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace DadPlanner2.Services;

public sealed class ReportDocumentService
{
    public ReportDocumentService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void Generate(
        ReportData reportData,
        string pdfPath,
        SupplementAnalysisResult? supplementAnalysis = null)
    {
        var logs = reportData.Logs;
        var gapData = reportData.GapData;
        double avgGap = reportData.AverageGap;
        double minGap = reportData.MinimumGap;

        // Line Chart (Recovery Gap Timeline)
        var lineChart = new SKCartesianChart
        {
            Width = 900, Height = 250,
            Series = new ISeries[] {
                new LineSeries<DateTimePoint> {
                    Values = gapData,
                    Fill = new SolidColorPaint(new SKColor(0, 122, 204, 50)),
                    Stroke = new SolidColorPaint(new SKColor(0, 122, 204)) { StrokeThickness = 2 },
                    GeometrySize = 6
                }
            },
            XAxes = new[] { new Axis { Labeler = val => new DateTime((long)val).ToString("MMM dd"), LabelsPaint = new SolidColorPaint(SKColors.Black) } },
            YAxes = new[] { new Axis { Name = "Gap (Hrs)", LabelsPaint = new SolidColorPaint(SKColors.Black), NamePaint = new SolidColorPaint(SKColors.Black) } },
            Background = SKColors.White
        };

        byte[] lineBytes;
        using (var img = lineChart.GetImage())
        using (var data = img.Encode(SKEncodedImageFormat.Png, 100)) lineBytes = data.ToArray();

        int maint = reportData.MaintenanceCount;
        int play = reportData.PlaytimeCount;
        int baby = reportData.BabyMakingCount;
        int lab = reportData.ClinicalLabCount;

        // Pie Chart (Event Distribution)
        var pieChart = new SKPieChart
        {
            Width = 450, Height = 300,
            Series = new ISeries[] {
                new PieSeries<int> { Values = new[] { maint }, Name = "Maintenance", Fill = new SolidColorPaint(new SKColor(0, 122, 204)) },
                new PieSeries<int> { Values = new[] { play }, Name = "Playtime", Fill = new SolidColorPaint(new SKColor(156, 39, 176)) },
                new PieSeries<int> { Values = new[] { baby }, Name = "Baby-Making", Fill = new SolidColorPaint(new SKColor(76, 175, 80)) },
                new PieSeries<int> { Values = new[] { lab }, Name = "Clinical", Fill = new SolidColorPaint(new SKColor(84, 110, 122)) }
            },
            Background = SKColors.White,
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
            LegendTextPaint = new SolidColorPaint(SKColors.Black)
        };

        byte[] pieBytes;
        using (var img = pieChart.GetImage())
        using (var data = img.Encode(SKEncodedImageFormat.Png, 100)) pieBytes = data.ToArray();

        int high = reportData.HighCount;
        int norm = reportData.NormalCount;
        int low = reportData.LowCount;
        int dry = reportData.DryCount;

        // Bar Chart (Yield Profile)
        var barChart = new SKCartesianChart
        {
            Width = 450, Height = 300,
            Series = new ISeries[] {
                new ColumnSeries<int> {
                    Values = new[] { dry, low, norm, high },
                    Fill = new SolidColorPaint(new SKColor(0, 122, 204)),
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Middle,
                    DataLabelsFormatter = p => p.Model > 0 ? p.Model.ToString() : ""
                }
            },
            XAxes = new[] { new Axis { Labels = new[] { "Dry", "Low", "Normal", "High" }, LabelsPaint = new SolidColorPaint(SKColors.Black) } },
            YAxes = new[] { new Axis { LabelsPaint = new SolidColorPaint(SKColors.Black), MinLimit = 0 } },
            Background = SKColors.White
        };

        byte[] barBytes;
        using (var img = barChart.GetImage())
        using (var data = img.Encode(SKEncodedImageFormat.Png, 100)) barBytes = data.ToArray();

        string pdfFont = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Liberation Sans" :
                         RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Helvetica" : Fonts.Arial;

        // Calculate 90-day Thermal Status
        var heatEvents = logs.Where(l => l.HeatFlag >= 2).OrderByDescending(l => l.Timestamp).ToList();
        string thermalStatus = "Uncompromised (No Severe Heat in Window)";
        if (heatEvents.Count > 0)
        {
            var latest = heatEvents[0];
            long elapsedDays = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - latest.Timestamp) / 86400;
            if (elapsedDays < 74)
            {
                thermalStatus = $"Active Thermal Shadow (Day {elapsedDays}/74 - L{latest.HeatFlag} Heat)";
            }
        }

        // Calculate Optimal Window Compliance (gaps between 24h and 72h)
        var releaseLogs = logs.Where(l => l.Volume != "None" && l.Volume != "N/A").OrderBy(l => l.Timestamp).ToList();
        int inWindowCount = 0;
        int totalGaps = 0;
        for (int i = 0; i < releaseLogs.Count - 1; i++)
        {
            double gapH = (releaseLogs[i + 1].Timestamp - releaseLogs[i].Timestamp) / 3600.0;
            if (gapH >= 24.0 && gapH <= 72.0) inWindowCount++;
            totalGaps++;
        }
        string complianceRate = totalGaps == 0 ? "--" : $"{((double)inWindowCount / totalGaps) * 100:F0}%";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(pdfFont));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("PIMS BASELINE REPORT").SemiBold().FontSize(20).FontColor(Colors.Blue.Darken2);
                        col.Item().Text("Reproductive System Analytics & Clinical Summary").FontSize(12).FontColor(Colors.Grey.Darken1);
                        col.Item().Text($"Thermal Status: {thermalStatus}").FontSize(9).FontColor(heatEvents.Count > 0 ? Colors.Orange.Darken2 : Colors.Green.Darken2).SemiBold();
                    });
                    row.RelativeItem().AlignRight().Column(col =>
                    {
                        col.Item().Text($"Date: {DateTime.Now:MMM dd, yyyy}").SemiBold();
                        col.Item().Text("Cycle: 90-Day Retrospective");
                        col.Item().Text("WHO Reference: 6th Ed. (2021)").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });

                page.Content().PaddingVertical(0.8f, Unit.Centimetre).Column(col =>
                {
                    // 4-Column Summary Cards
                    col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(10).Row(row =>
                    {
                        row.RelativeItem().Column(c => {
                            c.Item().Text("Total Cycles").SemiBold().FontColor(Colors.Grey.Darken1);
                            c.Item().Text(logs.Count.ToString()).FontSize(16).SemiBold();
                        });
                        row.RelativeItem().Column(c => {
                            c.Item().Text("Mean Recovery").SemiBold().FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"{avgGap:F1} Hrs").FontSize(16).SemiBold();
                        });
                        row.RelativeItem().Column(c => {
                            c.Item().Text("Min Recovery").SemiBold().FontColor(Colors.Grey.Darken1);
                            c.Item().Text(minGap == 999 ? "--" : $"{minGap:F1} Hrs").FontSize(16).SemiBold();
                        });
                        row.RelativeItem().Column(c => {
                            c.Item().Text("Optimal Gap Rate").SemiBold().FontColor(Colors.Grey.Darken1);
                            c.Item().Text(complianceRate).FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);
                        });
                    });

                    col.Item().PaddingTop(10);

                    if (logs.Count == 0)
                    {
                        col.Item().Text("No records found in the last 90 days.").Italic();
                    }
                    else
                    {
                        col.Item().PaddingBottom(15).Column(c => {
                            c.Item().Text("Recovery Gap Timeline (Hours Between Releases)").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken2);
                            c.Item().Image(lineBytes);
                        });

                        col.Item().PaddingBottom(15).Row(r => {
                            r.RelativeItem().PaddingRight(5).Column(c => {
                                c.Item().Text("Event Distribution").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken2);
                                c.Item().Image(pieBytes);
                            });
                            r.RelativeItem().PaddingLeft(5).Column(c => {
                                c.Item().Text("Yield Profile").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken2);
                                c.Item().Image(barBytes);
                            });
                        });

                        col.Item().PaddingBottom(5).Text("Raw Event Log & Clinical Analysis").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken2);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.ConstantColumn(38);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(7);
                            });

                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Date").SemiBold();
                                header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Mode").SemiBold();
                                header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Vol").SemiBold();
                                header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Count").SemiBold();
                                header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Confidence").SemiBold();
                                header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Supplements").SemiBold();
                                header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Lab Results & Abstinence").SemiBold();
                            });

                            bool isAlternate = false;
                            var sortedLogs = logs.OrderBy(l => l.Timestamp).ToList();

                            for (int i = 0; i < sortedLogs.Count; i++)
                            {
                                var log = sortedLogs[i];
                                var backgroundColor = isAlternate ? Colors.Grey.Lighten4 : Colors.White;
                                if (log.Mode == "Clinical-Lab") backgroundColor = Colors.Blue.Lighten4;

                                var date = DateTimeOffset.FromUnixTimeSeconds(log.Timestamp).ToLocalTime().ToString("MMM dd HH:mm");

                                string suppStr = "";
                                if (log.Supplements.Contains("\"zinc\":1")) suppStr += "💊 ";
                                if (log.Supplements.Contains("\"maca\":1")) suppStr += "🌿 ";
                                if (log.Supplements.Contains("\"vitD\":1")) suppStr += "☀️ ";
                                if (log.Supplements.Contains("\"vitC\":1")) suppStr += "🍊 ";

                                string heatStr = log.HeatFlag > 0 ? $"🔥 L{log.HeatFlag}" : "";
                                string combinedSupps = (heatStr + " " + suppStr).Trim();
                                if (string.IsNullOrEmpty(combinedSupps)) combinedSupps = "-";

                                string labStr = "-";

                                if (log.Mode == "Clinical-Lab" || log.Concentration > 0 || log.Motility > 0 || log.Morphology > 0)
                                {
                                    // Calculate exact abstinence prior to this lab test
                                    string abstTag = "";
                                    var priorRelease = sortedLogs.Take(i).Where(l => l.Volume != "None" && l.Volume != "N/A").LastOrDefault();
                                    if (priorRelease != null)
                                    {
                                        double abstHours = (log.Timestamp - priorRelease.Timestamp) / 3600.0;
                                        string whoTag = (abstHours >= 48.0 && abstHours <= 72.0) ? "WHO Ideal" :
                                                        (abstHours >= 48.0 && abstHours <= 168.0) ? "WHO Acceptable" : "Outside WHO Rec";
                                        abstTag = $" [Abst: {abstHours:F0}h ({whoTag})]";
                                    }

                                    labStr = $"Vol: {log.ClinicalVol:F1}mL | C: {log.Concentration}M | Mot: {log.Motility}% (P:{log.ProgMotility}%) | Mor: {log.Morphology}% | pH: {log.PhLevel:F1}{abstTag}";
                                }

                                table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(date).FontSize(9);
                                table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(log.Mode).FontSize(9);
                                table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(log.Volume).FontSize(9);
                                table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(log.ReleaseCount.ToString()).FontSize(9);
                                table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(log.VolumeConfidence.ToString()).FontSize(8);
                                table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(combinedSupps).FontSize(9);
                                table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(labStr).FontSize(8).SemiBold();

                                isAlternate = !isAlternate;
                            }
                        });

                        if (supplementAnalysis != null)
                        {
                            col.Item().PaddingTop(15).Text("Observed Supplement Associations & Saturation")
                                .SemiBold().FontSize(12).FontColor(Colors.Grey.Darken2);
                            col.Item().Text("Personal observational comparisons only; not clinical evidence. "
                                + "Evaluates 50% target presence and exponential steady-state saturation. "
                                + "Confidence counts use O=Observed, E=Estimated, U=Unknown.")
                                .FontSize(8).Italic();

                            col.Item().PaddingTop(5).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(1.7f);
                                    columns.RelativeColumn(1.7f);
                                    columns.RelativeColumn(1.7f);
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(3);
                                });
                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Black).Text("Supplement").SemiBold();
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Black).Text("Window").SemiBold();
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Black).Text("Saturated").SemiBold();
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Black).Text("Unsaturated").SemiBold();
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Black).Text("Successes").SemiBold();
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Black).Text("Proportion").SemiBold();
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Black).Text("Targets").SemiBold();
                                });

                                foreach (var comparison in new[]
                                {
                                    supplementAnalysis.Zinc,
                                    supplementAnalysis.Maca,
                                    supplementAnalysis.VitaminD,
                                    supplementAnalysis.VitaminC
                                })
                                {
                                    int releaseEvents = comparison.SaturationAudits.Sum(audit => audit.ReleaseEventCount);
                                    int supplementEvents = comparison.SaturationAudits.Sum(audit => audit.SupplementEventCount);
                                    double proportion = releaseEvents == 0 ? 0 : supplementEvents / (double)releaseEvents;
                                    string targets = string.Join(", ", comparison.SaturationAudits.Select(audit =>
                                        $"{DateTimeOffset.FromUnixTimeSeconds(audit.TargetTimestamp).ToLocalTime():MMM dd} "
                                        + (audit.IsSaturated ? "S" : "U")));

                                    table.Cell().PaddingVertical(3).Text(comparison.Supplement);
                                    table.Cell().PaddingVertical(3).Text($"{comparison.WindowDays} days");
                                    table.Cell().PaddingVertical(3).Text(
                                        $"{comparison.SaturatedCount} ({FormatConfidenceCounts(comparison.SaturatedConfidenceCounts)})");
                                    table.Cell().PaddingVertical(3).Text(
                                        $"{comparison.UnsaturatedCount} ({FormatConfidenceCounts(comparison.UnsaturatedConfidenceCounts)})");
                                    table.Cell().PaddingVertical(3).Text(
                                        FormatSuccessCounts(comparison));
                                    table.Cell().PaddingVertical(3).Text($"{proportion:P1}");
                                    table.Cell().PaddingVertical(3).Text(targets.Length == 0 ? "No target events" : targets);
                                }
                            });
                        }
                    }
                });

                page.Footer().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("CONFIDENTIAL MEDICAL RECORD").FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            });
        });

        document.GeneratePdf(pdfPath);
    }

    private static string FormatConfidenceCounts(ConfidenceCounts counts) =>
        $"O:{counts.Observed} E:{counts.Estimated} U:{counts.Unknown}";

    private static string FormatSuccessCounts(SupplementComparison comparison)
    {
        if (!comparison.SaturatedSuccesses.HasValue && !comparison.UnsaturatedSuccesses.HasValue)
            return "-";

        return $"S {FormatConfidenceCounts(comparison.SaturatedSuccessesByConfidence)} / "
            + $"U {FormatConfidenceCounts(comparison.UnsaturatedSuccessesByConfidence)}";
    }
}
