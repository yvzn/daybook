using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>
/// Next 1:1, in meeting order: progress on each objective, then issues and asks. Only what is still to share
/// shows here; shared subjects live in "Shared earlier".
/// </summary>
public partial class OneOnOneViewModel : ObservableObject
{
    private const int QuietAfterMeetings = 3;

    private readonly ToastViewModel _toast;
    private readonly Action _showSharedEarlier;
    private readonly Action _editObjectives;
    private Semester? _semester;

    public OneOnOneViewModel(OneOnOneBook book, ToastViewModel toast, Action showSharedEarlier, Action editObjectives)
    {
        Book = book;
        _toast = toast;
        _showSharedEarlier = showSharedEarlier;
        _editObjectives = editObjectives;
        Book.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OneOnOneBook.Current)) Refresh();
        };
        Book.Issues.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasIssues));
        Refresh();
    }

    public OneOnOneBook Book { get; }

    public ObservableCollection<ObjectiveRowViewModel> Objectives { get; } = [];

    public string SemesterLabel => $"· {Book.Current.Label}";

    public bool HasObjectives => Book.Current.Objectives.Count > 0;

    public bool HasIssues => Book.Issues.Count > 0;

    public bool ShowReviewNudge => Book.IsReviewSeason;

    [ObservableProperty]
    private string _newIssue = "";

    /// <summary>Rebuild rows: the semester, its objectives or the meeting count may have changed.</summary>
    public void Refresh()
    {
        if (_semester is not null) _semester.Objectives.CollectionChanged -= OnObjectivesChanged;
        _semester = Book.Current;
        _semester.Objectives.CollectionChanged += OnObjectivesChanged;

        foreach (var row in Objectives) row.Detach();
        Objectives.Clear();
        foreach (var objective in Book.Current.AllGroups)
            Objectives.Add(new ObjectiveRowViewModel(this, objective, isOther: objective == Book.Current.Other));

        OnPropertyChanged(nameof(SemesterLabel));
        OnPropertyChanged(nameof(HasObjectives));
        OnPropertyChanged(nameof(ShowReviewNudge));
    }

    internal string? QuietHint(Objective objective)
    {
        if (objective == Book.Current.Other) return null; // quiet there is normal
        if (objective.Subjects.Any(s => !s.IsShared)) return null; // about to be mentioned
        var count = Book.MeetingsWithout(objective);
        return count >= QuietAfterMeetings ? $"Not mentioned in last {count} 1:1s" : null;
    }

    [RelayCommand]
    private void AddIssue()
    {
        var text = NewIssue.Trim();
        if (text.Length == 0) return;

        Book.Issues.Add(new Issue(text, WorkDays.Today));
        NewIssue = "";
    }

    [RelayCommand]
    private void DeleteSubject(OneOnOneSubject subject)
    {
        var objective = Book.Current.AllGroups.FirstOrDefault(o => o.Subjects.Contains(subject));
        if (objective is null) return;

        var index = objective.Subjects.IndexOf(subject);
        objective.Subjects.RemoveAt(index);
        _toast.Show($"\"{subject.Text}\" deleted",
            () => objective.Subjects.Insert(Math.Min(index, objective.Subjects.Count), subject));
    }

    [RelayCommand]
    private void DeleteIssue(Issue issue)
    {
        var index = Book.Issues.IndexOf(issue);
        if (index < 0) return;

        Book.Issues.RemoveAt(index);
        _toast.Show($"\"{issue.Text}\" deleted", () => Book.Issues.Insert(Math.Min(index, Book.Issues.Count), issue));
    }

    /// <summary>
    /// After the meeting: ticked subjects become "shared on today", ticked issues go, open issues count one more 1:1.
    /// </summary>
    [RelayCommand]
    private void Done()
    {
        var today = WorkDays.Today;
        var shared = Book.Current.AllGroups.SelectMany(o => o.Subjects).Where(s => s.IsTicked && !s.IsShared).ToList();
        var issuesBefore = Book.Issues.ToList();
        var resolved = issuesBefore.Where(i => i.IsTicked).ToList();
        var open = issuesBefore.Except(resolved).ToList();

        // Pressing it twice on the same day is still one meeting.
        var isNewMeeting = Book.Meetings.LastOrDefault() != today;

        foreach (var subject in shared)
        {
            subject.SharedOn = today;
            subject.IsTicked = false;
        }
        foreach (var issue in resolved) Book.Issues.Remove(issue);
        if (isNewMeeting)
        {
            foreach (var issue in open) issue.TimesRaised++;
            Book.Meetings.Add(today);
        }
        Refresh();

        var parts = new List<string> { "1:1 done" };
        if (shared.Count > 0) parts.Add($"{shared.Count} shared");
        if (resolved.Count > 0) parts.Add(resolved.Count == 1 ? "1 issue closed" : $"{resolved.Count} issues closed");

        _toast.Show(string.Join(" · ", parts), () =>
        {
            foreach (var subject in shared)
            {
                subject.SharedOn = null;
                subject.IsTicked = true;
            }
            Book.Issues.Clear();
            foreach (var issue in issuesBefore) Book.Issues.Add(issue);
            if (isNewMeeting)
            {
                foreach (var issue in open) issue.TimesRaised--;
                Book.Meetings.RemoveAt(Book.Meetings.Count - 1);
            }
            Refresh();
        });
    }

    [RelayCommand]
    private void ShowSharedEarlier() => _showSharedEarlier();

    [RelayCommand]
    private void EditObjectives() => _editObjectives();

    private void OnObjectivesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
}

/// <summary>One objective on the 1:1 page: what is still to share, its own add box, a hint when it went quiet.</summary>
public partial class ObjectiveRowViewModel : ObservableObject
{
    private readonly OneOnOneViewModel _owner;

    public ObjectiveRowViewModel(OneOnOneViewModel owner, Objective objective, bool isOther)
    {
        _owner = owner;
        Objective = objective;
        IsOther = isOther;
        Objective.Subjects.CollectionChanged += OnSubjectsChanged;
        foreach (var subject in Objective.Subjects) subject.PropertyChanged += OnSubjectChanged;
        Fill();
    }

    public Objective Objective { get; }

    /// <summary>"Outside objectives": built in, styled apart.</summary>
    public bool IsOther { get; }

    public ObservableCollection<OneOnOneSubject> Pending { get; } = [];

    public string? QuietHint => _owner.QuietHint(Objective);

    public bool IsQuiet => QuietHint is not null;

    [ObservableProperty]
    private string _newText = "";

    [RelayCommand]
    private void Add()
    {
        var text = NewText.Trim();
        if (text.Length == 0) return;

        Objective.Subjects.Add(new OneOnOneSubject(text, WorkDays.Today));
        NewText = "";
    }

    internal void Detach()
    {
        Objective.Subjects.CollectionChanged -= OnSubjectsChanged;
        foreach (var subject in Objective.Subjects) subject.PropertyChanged -= OnSubjectChanged;
    }

    private void OnSubjectsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (OneOnOneSubject s in e.OldItems ?? Array.Empty<OneOnOneSubject>()) s.PropertyChanged -= OnSubjectChanged;
        foreach (OneOnOneSubject s in e.NewItems ?? Array.Empty<OneOnOneSubject>()) s.PropertyChanged += OnSubjectChanged;
        Fill();
    }

    private void OnSubjectChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OneOnOneSubject.SharedOn)) Fill();
    }

    private void Fill()
    {
        var wanted = Objective.Subjects.Where(s => !s.IsShared).ToList();
        if (!Pending.SequenceEqual(wanted))
        {
            Pending.Clear();
            foreach (var subject in wanted) Pending.Add(subject);
        }
        OnPropertyChanged(nameof(QuietHint));
        OnPropertyChanged(nameof(IsQuiet));
    }
}
