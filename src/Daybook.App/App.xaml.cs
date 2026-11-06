using System.Windows;
using Daybook.App.Services;
using Daybook.App.ViewModels;

namespace Daybook.App;

public partial class App : Application
{
    private readonly TeamStore _teamStore = new();
    private readonly TaskStore _taskStore = new();
    private readonly RetroStore _retroStore = new();
    private readonly OneOnOneStore _oneOnOneStore = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new MainWindow
        {
            DataContext = new MainViewModel(_teamStore.Load(), _taskStore.Load(), _retroStore.Load(), _oneOnOneStore.Load()),
        }.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _teamStore.Flush();
        _taskStore.Flush();
        _retroStore.Flush();
        _oneOnOneStore.Flush();
        base.OnExit(e);
    }
}
