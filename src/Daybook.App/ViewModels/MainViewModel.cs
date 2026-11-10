using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>
/// Navigation: Today | Standup | Retro | 1:1 tabs. Edit team is a page under Standup; Semester review and
/// Objectives are pages under 1:1.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly OneOnOneBook _oneOnOneBook;

    public ToastViewModel Toast { get; } = new();
    public TodayViewModel Today { get; }
    public StandupNotesViewModel Notes { get; }
    public StandupViewModel Standup { get; }
    public EditTeamViewModel EditTeam { get; }
    public RetroViewModel Retro { get; }
    public OneOnOneViewModel OneOnOne { get; }
    public EditObjectivesViewModel EditObjectives { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTodayTab), nameof(IsStandupTab), nameof(IsRetroTab), nameof(IsOneOnOneTab))]
    private ObservableObject _currentPage;

    public bool IsTodayTab => CurrentPage == Today;
    public bool IsStandupTab => CurrentPage == Standup || CurrentPage == EditTeam;
    public bool IsRetroTab => CurrentPage == Retro;
    public bool IsOneOnOneTab => CurrentPage == OneOnOne || CurrentPage == EditObjectives || CurrentPage is SharedEarlierViewModel;

    public MainViewModel(Team team, TaskList tasks, RetroList retro, OneOnOneBook oneOnOne)
    {
        _oneOnOneBook = oneOnOne;
        Today = new TodayViewModel(tasks, oneOnOne, Toast);
        Notes = new StandupNotesViewModel(tasks);
        Standup = new StandupViewModel(team, Notes, Toast, () => CurrentPage = EditTeam!);
        EditTeam = new EditTeamViewModel(team, Toast, ShowStandup);
        Retro = new RetroViewModel(retro, Toast);
        OneOnOne = new OneOnOneViewModel(oneOnOne, Toast, () => OpenSemesterReview(oneOnOne.Current), ShowEditObjectives);
        EditObjectives = new EditObjectivesViewModel(oneOnOne, Toast, ShowOneOnOne);
        _currentPage = Today;
    }

    partial void OnCurrentPageChanging(ObservableObject value)
    {
        if (CurrentPage is SharedEarlierViewModel leaving) leaving.Detach();
    }

    [RelayCommand]
    private void ShowRetro() => CurrentPage = Retro;

    [RelayCommand]
    private void ShowOneOnOne()
    {
        OneOnOne.Refresh();
        CurrentPage = OneOnOne;
    }

    private void ShowEditObjectives()
    {
        EditObjectives.Refresh();
        CurrentPage = EditObjectives;
    }

    /// <summary>The current semester (back to 1:1), or a past one (back to the current semester).</summary>
    private void OpenSemesterReview(Semester semester)
    {
        var isCurrent = semester == _oneOnOneBook.Current;
        CurrentPage = new SharedEarlierViewModel(_oneOnOneBook, semester, Toast,
            back: isCurrent ? ShowOneOnOne : () => OpenSemesterReview(_oneOnOneBook.Current),
            openSemester: OpenSemesterReview,
            semesterStarted: ShowOneOnOne);
    }

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
