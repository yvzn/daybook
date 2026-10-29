using System.Windows;
using Daybook.App.Services;
using Daybook.App.ViewModels;

namespace Daybook.App;

public partial class App : Application
{
    private readonly TeamStore _store = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new MainWindow { DataContext = new MainViewModel(_store.Load()) }.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _store.Flush();
        base.OnExit(e);
    }
}
