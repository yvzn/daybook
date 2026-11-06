using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>Navigation: Today | Standup | Retro tabs. Edit team is a page under Standup.</summary>
public partial class MainViewModel : ObservableObject
{
    public ToastViewModel Toast { get; } = new();
    public TodayViewModel Today { get; }
    public StandupNotesViewModel Notes { get; }
    public StandupViewModel Standup { get; }
    public EditTeamViewModel EditTeam { get; }
    public RetroViewModel Retro { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTodayTab), nameof(IsStandupTab), nameof(IsRetroTab))]
    private ObservableObject _currentPage;

    public bool IsTodayTab => CurrentPage == Today;
    public bool IsStandupTab => CurrentPage == Standup || CurrentPage == EditTeam;
    public bool IsRetroTab => CurrentPage == Retro;

    public MainViewModel(Team team, TaskList tasks, RetroList retro)
    {
        Today = new TodayViewModel(tasks, Toast);
        Notes = new StandupNotesViewModel(tasks);
        Standup = new StandupViewModel(team, Notes, Toast, () => CurrentPage = EditTeam!);
        EditTeam = new EditTeamViewModel(team, Toast, ShowStandup);
        Retro = new RetroViewModel(retro, Toast);
        _currentPage = Today;
    }

    [RelayCommand]
    private void ShowRetro() => CurrentPage = Retro;

    /// <summary>Window got focus: pick up a new day if the app stayed open overnight.</summary>
    public void OnWindowActivated() => Today.RefreshDay();

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
