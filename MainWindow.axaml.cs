using Avalonia.Controls;
using DadPlanner2.ViewModels;

namespace DadPlanner2;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // This links the C# data engine to the AXAML visual layout
        DataContext = new MainWindowViewModel();
    }
}
