using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using DadPlanner2.ViewModels;

namespace DadPlanner2;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        var vm = new MainWindowViewModel();
        DataContext = vm;
        Closing += (_, _) => vm.HandleShutdown();

        vm.RequestScrollToLog = (log) =>
        {
            var grid = this.FindControl<DataGrid>("LogsGrid");
            grid?.ScrollIntoView(log, null);
        };
    }

    private void MainContainer_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // GATEKEEPER: If any modal dialog is open, kill the tooltips and ignore hover logic
        if (vm.IsHelpOpen || vm.IsSettingsOpen || vm.IsManualLogOpen || vm.IsEditLogOpen || vm.IsAlertOpen)
        {
            vm.IsTooltipVisible = false;
            return;
        }

        var windowPos = e.GetPosition(this);
        bool handled = false;

        // 1. Timeline Chart Hover
        var timeline = this.FindControl<LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart>("MainChart");
        if (timeline != null && timeline.CoreChart is LiveChartsCore.Chart<LiveChartsCore.SkiaSharpView.Drawing.SkiaSharpDrawingContext> timelineCore)
        {
            var tlPoint = timeline.TranslatePoint(new Point(0, 0), this);
            if (tlPoint.HasValue)
            {
                var p = e.GetPosition(timeline);
                
                double ctrlX = tlPoint.Value.X;
                double ctrlY = tlPoint.Value.Y;
                double ctrlW = timeline.Bounds.Width;
                double ctrlH = timeline.Bounds.Height;
                
                double plotX = timelineCore.DrawMarginLocation.X;
                double plotY = timelineCore.DrawMarginLocation.Y;
                double plotW = timelineCore.DrawMarginSize.Width;
                double plotH = timelineCore.DrawMarginSize.Height;
                
                if (p.X >= plotX && p.X <= plotX + plotW && p.Y >= plotY && p.Y <= plotY + plotH)
                {
                    vm.ProcessTimelineHover(p.X, windowPos.X, windowPos.Y, plotX, plotW, ctrlX, ctrlY, ctrlW, ctrlH);
                    handled = true;
                }
            }
        }

        // 2. Pie Chart Hover
        if (!handled)
        {
            var pie = this.FindControl<LiveChartsCore.SkiaSharpView.Avalonia.PieChart>("PieChart");
            if (pie != null && pie.CoreChart is LiveChartsCore.Chart<LiveChartsCore.SkiaSharpView.Drawing.SkiaSharpDrawingContext> pieCore)
            {
                var tlPoint = pie.TranslatePoint(new Point(0, 0), this);
                if (tlPoint.HasValue)
                {
                    var p = e.GetPosition(pie);
                    
                    double ctrlX = tlPoint.Value.X;
                    double ctrlY = tlPoint.Value.Y;
                    double ctrlW = pie.Bounds.Width;
                    double ctrlH = pie.Bounds.Height;

                    double plotX = pieCore.DrawMarginLocation.X;
                    double plotY = pieCore.DrawMarginLocation.Y;
                    double plotW = pieCore.DrawMarginSize.Width;
                    double plotH = pieCore.DrawMarginSize.Height;

                    if (p.X >= plotX && p.X <= plotX + plotW && p.Y >= plotY && p.Y <= plotY + plotH)
                    {
                        vm.ProcessPieHover(p.X, p.Y, windowPos.X, windowPos.Y, plotX, plotY, plotW, plotH, ctrlX, ctrlY, ctrlW, ctrlH);
                        handled = true;
                    }
                }
            }
        }

        // 3. Bar Chart Hover
        if (!handled)
        {
            var bar = this.FindControl<LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart>("BarChart");
            if (bar != null && bar.CoreChart is LiveChartsCore.Chart<LiveChartsCore.SkiaSharpView.Drawing.SkiaSharpDrawingContext> barCore)
            {
                var tlPoint = bar.TranslatePoint(new Point(0, 0), this);
                if (tlPoint.HasValue)
                {
                    var p = e.GetPosition(bar);
                    
                    double ctrlX = tlPoint.Value.X;
                    double ctrlY = tlPoint.Value.Y;
                    double ctrlW = bar.Bounds.Width;
                    double ctrlH = bar.Bounds.Height;

                    // Grab X and Width so we don't bleed into adjacent controls
                    double plotX = barCore.DrawMarginLocation.X;
                    double plotY = barCore.DrawMarginLocation.Y;
                    double plotW = barCore.DrawMarginSize.Width;
                    double plotH = barCore.DrawMarginSize.Height;
                    
                    // Strictly enforce both X and Y boundaries
                    if (p.X >= plotX && p.X <= plotX + plotW && p.Y >= plotY && p.Y <= plotY + plotH)
                    {
                        vm.ProcessBarHover(p.Y, windowPos.X, windowPos.Y, plotY, plotH, ctrlX, ctrlY, ctrlW, ctrlH);
                        handled = true;
                    }
                }
            }
        }

        if (!handled)
        {
            vm.IsTooltipVisible = false;
        }
    }
}

public class ModeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string mode)
        {
            return mode switch
            {
                "Maintenance" => new SolidColorBrush(Color.Parse("#007acc")),
                "Playtime" => new SolidColorBrush(Color.Parse("#9c27b0")),
                "Baby-Making" => new SolidColorBrush(Color.Parse("#4caf50")),
                "Clinical-Lab" => new SolidColorBrush(Color.Parse("#546e7a")),
                _ => new SolidColorBrush(Colors.White)
            };
        }
        return new SolidColorBrush(Colors.White);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}
