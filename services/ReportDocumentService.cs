using System;
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

    public void Generate(ReportData reportData, string pdfPath)
    {
        var logs = reportData.Logs;
        var gapData = reportData.GapData;
        double avgGap = reportData.AverageGap;
        double minGap = reportData.MinimumGap;

        var lineChart = new SKCartesianChart
        {
            Width = 900, Height = 250,
            Series = new ISeries[] { new LineSeries<DateTimePoint> { Values = gapData, Fill = new SolidColorPaint(new SKColor(0, 122, 204, 50)), Stroke = new SolidColorPaint(new SKColor(0, 122, 204)) { StrokeThickness = 2 }, GeometrySize = 6 } },
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
                        col.Item().Text("Reproductive System Analytics").FontSize(14).FontColor(Colors.Grey.Darken1);
                    });
                    row.RelativeItem().AlignRight().Column(col =>
                    {
                        col.Item().Text($"Date: {DateTime.Now:MMM dd, yyyy}").SemiBold();
                        col.Item().Text("Cycle: 90-Day Retrospective");
                    });
                });

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                {
                    col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(10).Row(row =>
                    {
                        row.RelativeItem().Column(c => {
                            c.Item().Text("Total Cycles Recorded").SemiBold().FontColor(Colors.Grey.Darken1);
                            c.Item().Text(logs.Count.ToString()).FontSize(16).SemiBold();
                        });
                        row.RelativeItem().Column(c => {
                            c.Item().Text("Mean Recovery Gap").SemiBold().FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"{avgGap:F1} Hrs").FontSize(16).SemiBold();
                        });
                        row.RelativeItem().Column(c => {
                            c.Item().Text("Min Recovery Gap").SemiBold().FontColor(Colors.Grey.Darken1);
                            c.Item().Text(minGap == 999 ? "--" : $"{minGap:F1} Hrs").FontSize(16).SemiBold();
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
                            c.Item().Text("Recovery Gap Timeline").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken2);
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

                        col.Item().PaddingBottom(5).Text("Raw Event Log").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken2);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(7);
                            });

                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Date").SemiBold();
                                header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Mode").SemiBold();
                                header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Vol").SemiBold();
                                header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Supplements").SemiBold();
                                header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Lab Results").SemiBold();
                            });

                            bool isAlternate = false;
                            foreach (var log in logs)
                            {
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

                                if (log.Concentration > 0 || log.Motility > 0 || log.Morphology > 0)
                                {
                                    labStr = $"Vol: {log.ClinicalVol:F1}mL | C: {log.Concentration}M | Mot: {log.Motility}% (P:{log.ProgMotility}%) | Mor: {log.Morphology}% | pH: {log.PhLevel:F1}";
                                }

                                table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(date).FontSize(9);
                                table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(log.Mode).FontSize(9);
                                table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(log.Volume).FontSize(9);
                                table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(combinedSupps).FontSize(9);
                                table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(labStr).FontSize(8).SemiBold();

                                isAlternate = !isAlternate;
                            }
                        });
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

}
