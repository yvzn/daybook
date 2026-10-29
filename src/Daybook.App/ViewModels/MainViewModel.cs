using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>Navigation: Today | Standup tabs. Edit team is a page under Standup.</summary>
public partial class MainViewModel : ObservableObject
{
    public ToastViewModel Toast { get; } = new();
    public TodayViewModel Today { get; }
    public StandupNotesViewModel Notes { get; }
    public StandupViewModel Standup { get; }
    public EditTeamViewModel EditTeam { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTodayTab), nameof(IsStandupTab))]
    private ObservableObject _currentPage;

    public bool IsTodayTab => CurrentPage == Today;
    public bool IsStandupTab => !IsTodayTab;

    public MainViewModel(Team team, TaskList tasks)
    {
        Today = new TodayViewModel(tasks, Toast);
        Notes = new StandupNotesViewModel(tasks);
        Standup = new StandupViewModel(team, Notes, Toast, () => CurrentPage = EditTeam!);
        EditTeam = new EditTeamViewModel(team, Toast, ShowStandup);
        _currentPage = Today;
    }

    [RelayCommand]
    private void ShowToday() => CurrentPage = Today;

    [RelayCommand]
    private void ShowStandup()
    {
        Standup.SyncWithTeam();
        Notes.Refresh();
        CurrentPage = Standup;
    }
}
