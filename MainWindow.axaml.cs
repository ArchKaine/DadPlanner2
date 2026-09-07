using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
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

        vm.RequestScrollToLog = (log) =>
        {
            var grid = this.FindControl<DataGrid>("LogsGrid");
            grid?.ScrollIntoView(log, null);
        };
    }
}

// Converts the Mode string into the matching system color for the DataGrid
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