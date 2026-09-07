using Avalonia.Controls;
using DadPlanner2.ViewModels;

namespace DadPlanner2;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        var vm = new MainWindowViewModel();
        DataContext = vm;

        // XAML actively listens for the ViewModel click event and physically moves the scrollbar
        vm.RequestScrollToLog = (log) =>
        {
            var grid = this.FindControl<DataGrid>("LogsGrid");
            grid?.ScrollIntoView(log, null);
        };
    }
}
