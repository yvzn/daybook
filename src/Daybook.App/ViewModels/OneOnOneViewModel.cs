using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>
/// Next 1:1, in two modes, like Standup:
/// Prep: everything, editable, grouped and collapsible, one add box at the bottom. "Start 1:1" goes to the call.
/// Call: read-only and big for screen share; issues first, then only objectives with something to share; tick
/// what was covered. "Done with 1:1" closes the meeting and goes back to prep.
/// Only what is still to share shows here; shared subjects live in "Semester review".
/// </summary>
public partial class OneOnOneViewModel : ObservableObject
{
    private const int QuietAfterMeetings = 3;

    private readonly ToastViewModel _toast;
    private readonly Action _showSemesterReview;
    private readonly Action _editObjectives;
    private Semester? _semester;

    public OneOnOneViewModel(OneOnOneBook book, ToastViewModel toast, Action showSemesterReview, Action editObjectives)
    {
        Book = book;
        _toast = toast;
        _showSemesterReview = showSemesterReview;
        _editObjectives = editObjectives;

        PrepAdd = new QuickAddViewModel(() => Book.Current.AllGroups, AddSubject, AddIssue);
        CallAdd = new QuickAddViewModel(() => [], addSubject: null, AddIssue);

        Book.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OneOnOneBook.Current)) Refresh();
            if (e.PropertyName == nameof(OneOnOneBook.IsInCall)) OnPropertyChanged(nameof(IsInCall));
        };
        Book.Issues.CollectionChanged += OnIssuesChanged;
        Refresh();
    }

    public OneOnOneBook Book { get; }

    public ObservableCollection<ObjectiveRowViewModel> Objectives { get; } = [];

    /// <summary>Add box in prep: an objective, or issues.</summary>
    public QuickAddViewModel PrepAdd { get; }

    /// <summary>Add box during the call: a new issue only, the rest waits for prep.</summary>
    public QuickAddViewModel CallAdd { get; }

    public bool IsInCall => Book.IsInCall;

    public string SemesterLabel => $"· {Book.Current.Label}";

    public bool HasObjectives => Book.Current.Objectives.Count > 0;

    public int IssueCount => Book.Issues.Count;

    public bool HasIssues => Book.Issues.Count > 0;

    /// <summary>Call view: an objective with something to share.</summary>
    public bool HasPendingSubjects => Objectives.Any(o => o.HasPending);

    /// <summary>Call view: nothing at all to go through.</summary>
    public bool IsCallEmpty => !HasIssues && !HasPendingSubjects;

    [ObservableProperty]
    private bool _isIssuesExpanded;

    public bool ShowReviewNudge => Book.IsReviewSeason;

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
        IsIssuesExpanded = HasIssues;
        PrepAdd.Sync();

        OnPropertyChanged(nameof(SemesterLabel));
        OnPropertyChanged(nameof(HasObjectives));
        OnPropertyChanged(nameof(ShowReviewNudge));
        OnRowChanged();
    }

    internal void OnRowChanged()
    {
        OnPropertyChanged(nameof(HasPendingSubjects));
        OnPropertyChanged(nameof(IsCallEmpty));
    }

    internal string? QuietHint(Objective objective)
    {
        if (objective == Book.Current.Other) return null; // quiet there is normal
        if (objective.Subjects.Any(s => !s.IsShared)) return null; // about to be mentioned
        var count = Book.MeetingsWithout(objective);
        return count >= QuietAfterMeetings ? $"Not mentioned in last {count} 1:1s" : null;
    }

    private void AddSubject(Objective objective, string text) =>
        objective.Subjects.Add(new OneOnOneSubject(text, WorkDays.Today));

    private void AddIssue(string text) => Book.Issues.Add(new Issue(text, WorkDays.Today));

    private void OnIssuesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Just added: open the group, so you see where it went.
        if (e.Action == NotifyCollectionChangedAction.Add) IsIssuesExpanded = true;
        OnPropertyChanged(nameof(IssueCount));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(IsCallEmpty));
    }

    [RelayCommand]
    private void ToggleIssues()
    {
        if (HasIssues) IsIssuesExpanded = !IsIssuesExpanded;
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

    [RelayCommand]
    private void StartCall() => Book.IsInCall = true;

    /// <summary>Meeting cancelled or moved: back to prep, ticks kept, nothing recorded.</summary>
    [RelayCommand]
    private void BackToPrep() => Book.IsInCall = false;

    /// <summary>
    /// After the meeting: ticked subjects become "shared on today", ticked issues go, open issues count one more 1:1.
    /// Back to prep.
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
        Book.IsInCall = false;
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
            Book.IsInCall = true;
            Refresh();
        });
    }

    [RelayCommand]
    private void ShowSemesterReview() => _showSemesterReview();

    [RelayCommand]
    private void EditObjectives() => _editObjectives();

    private void OnObjectivesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
}

/// <summary>One objective on the 1:1 page: what is still to share, open or folded, a hint when it went quiet.</summary>
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
        IsExpanded = HasPending;
    }

    public Objective Objective { get; }

    /// <summary>"Outside objectives": built in, styled apart.</summary>
    public bool IsOther { get; }

    public ObservableCollection<OneOnOneSubject> Pending { get; } = [];

    public int Count => Pending.Count;

    /// <summary>Something to share. In the call, the others are hidden.</summary>
    public bool HasPending => Pending.Count > 0;

    /// <summary>Prep: subjects showing. Opens by itself when it has some, or gets a new one.</summary>
    [ObservableProperty]
    private bool _isExpanded;

    public string? QuietHint => _owner.QuietHint(Objective);

    public bool IsQuiet => QuietHint is not null;

    [RelayCommand]
    private void Toggle()
    {
        if (HasPending) IsExpanded = !IsExpanded;
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
        // Just added: open the group, so you see where it went.
        if (e.Action == NotifyCollectionChangedAction.Add) IsExpanded = true;
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
        if (!HasPending) IsExpanded = false;
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(QuietHint));
        OnPropertyChanged(nameof(IsQuiet));
        _owner.OnRowChanged();
    }
}
