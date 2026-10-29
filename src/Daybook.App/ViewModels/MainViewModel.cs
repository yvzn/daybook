using CommunityToolkit.Mvvm.ComponentModel;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ToastViewModel Toast { get; } = new();
    public StandupViewModel Standup { get; }
    public EditTeamViewModel EditTeam { get; }

    [ObservableProperty]
    private ObservableObject _currentPage;

    public MainViewModel(Team team)
    {
        Standup = new StandupViewModel(team, Toast, () => CurrentPage = EditTeam!);
        EditTeam = new EditTeamViewModel(team, Toast, ShowStandup);
        _currentPage = Standup;
    }

    private void ShowStandup()
    {
        Standup.SyncWithTeam();
        CurrentPage = Standup;
    }
}
