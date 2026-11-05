using System.Windows;
using Daybook.App.ViewModels;

namespace Daybook.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Activated += (_, _) => (DataContext as MainViewModel)?.OnWindowActivated();
    }
}
