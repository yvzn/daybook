using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>
/// Everything shared this semester, grouped by objective, all editable: the semester review is this same page.
/// Not a copy: editing here edits the subjects themselves.
/// </summary>
public partial class SharedEarlierViewModel : ObservableObject
{
    private readonly OneOnOneBook _book;
    private readonly ToastViewModel _toast;
    private readonly Action _back;
    private readonly Action<Semester> _openSemester;
    private readonly Action _semesterStarted;

    public SharedEarlierViewModel(OneOnOneBook book, Semester semester, ToastViewModel toast,
        Action back, Action<Semester> openSemester, Action semesterStarted)
    {
        _book = book;
        Semester = semester;
        _toast = toast;
        _back = back;
        _openSemester = openSemester;
        _semesterStarted = semesterStarted;
        Groups = semester.AllGroups.Select(o => new SharedGroupViewModel(o)).ToList();
    }

    public Semester Semester { get; }

    public IReadOnlyList<SharedGroupViewModel> Groups { get; }

    public bool IsCurrent => Semester == _book.Current;

    public string Title => IsCurrent ? $"{Semester.Label} · shared so far" : $"{Semester.Label} · shared";

    public string BackLabel => IsCurrent ? "Back to 1:1" : $"Back to {_book.Current.Label}";

    public bool HasObjectives => Semester.Objectives.Count > 0;

    /// <summary>Only from the current semester: a past one is reached from here.</summary>
    public IEnumerable<Semester> PastSemesters => IsCurrent ? _book.Past : [];

    public bool HasPastSemesters => IsCurrent && _book.Past.Count > 0;

    [RelayCommand]
    private void Back() => _back();

    /// <summary>Leaving the page: stop listening to the subjects.</summary>
    public void Detach()
    {
        foreach (var group in Groups) group.Detach();
    }

    [RelayCommand]
    private void OpenSemester(Semester semester) => _openSemester(semester);

    public void SetDate(OneOnOneSubject subject, DateOnly date) => subject.SharedOn = date;

    [RelayCommand]
    private void Delete(OneOnOneSubject subject)
    {
        var objective = Semester.AllGroups.FirstOrDefault(o => o.Subjects.Contains(subject));
        if (objective is null) return;

        var index = objective.Subjects.IndexOf(subject);
        objective.Subjects.RemoveAt(index);
        _toast.Show($"\"{subject.Text}\" deleted",
            () => objective.Subjects.Insert(Math.Min(index, objective.Subjects.Count), subject));
    }

    /// <summary>Back to the 1:1 page, to bring it up again.</summary>
    [RelayCommand]
    private void NotDiscussed(OneOnOneSubject subject)
    {
        var sharedOn = subject.SharedOn;
        subject.SharedOn = null;
        _toast.Show($"\"{subject.Text}\" back to next 1:1", () => subject.SharedOn = sharedOn);
    }

    /// <summary>Plain text, ready to paste in an email or the review tool.</summary>
    [RelayCommand]
    private void Copy()
    {
        var text = new StringBuilder();
        text.AppendLine(Title);
        foreach (var group in Groups)
        {
            if (group.Objective == Semester.Other && group.Shared.Count == 0) continue;
            text.AppendLine().AppendLine(group.Objective.Title);
            if (group.Shared.Count == 0) text.AppendLine("- Nothing shared yet");
            foreach (var subject in group.Shared)
            {
                text.AppendLine($"- {subject.DateText}: {subject.Text}");
                if (subject.HasOutcome) text.AppendLine($"  → {subject.Outcome}");
            }
        }

        try
        {
            Clipboard.SetText(text.ToString());
            _toast.Show("Copied");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            _toast.Show("Clipboard busy, try again");
        }
    }

    /// <summary>
    /// After the review. Objectives carry over (edit them next), with what was still to share; the old semester
    /// stays readable from here.
    /// </summary>
    [RelayCommand]
    private void StartNewSemester()
    {
        var today = WorkDays.Today;
        var old = _book.Current;
        var next = new Semester(OneOnOneBook.LabelAfter(old, today), today);
        var moved = new List<(Objective From, OneOnOneSubject Subject)>();

        foreach (var objective in old.Objectives)
        {
            var copy = new Objective(objective.Title, today);
            foreach (var subject in objective.Subjects.Where(s => !s.IsShared).ToList())
            {
                objective.Subjects.Remove(subject);
                copy.Subjects.Add(subject);
                moved.Add((objective, subject));
            }
            next.Objectives.Add(copy);
        }
        foreach (var subject in old.Other.Subjects.Where(s => !s.IsShared).ToList())
        {
            old.Other.Subjects.Remove(subject);
            next.Other.Subjects.Add(subject);
            moved.Add((old.Other, subject));
        }

        _book.Past.Insert(0, old);
        _book.Current = next;
        _semesterStarted();

        _toast.Show($"{next.Label} started, objectives kept", () =>
        {
            foreach (var (from, subject) in moved) from.Subjects.Add(subject);
            _book.Past.Remove(old);
            _book.Current = old;
        });
    }
}

/// <summary>One objective in the review: what was shared, in date order.</summary>
public partial class SharedGroupViewModel : ObservableObject
{
    public SharedGroupViewModel(Objective objective)
    {
        Objective = objective;
        Objective.Subjects.CollectionChanged += OnSubjectsChanged;
        foreach (var subject in Objective.Subjects) subject.PropertyChanged += OnSubjectChanged;
        Fill();
    }

    public Objective Objective { get; }

    public ObservableCollection<OneOnOneSubject> Shared { get; } = [];

    public bool IsEmpty => Shared.Count == 0;

    [ObservableProperty]
    private string _newText = "";

    /// <summary>A forgotten win: counted as shared today; click its date to change it.</summary>
    [RelayCommand]
    private void Add()
    {
        var text = NewText.Trim();
        if (text.Length == 0) return;

        Objective.Subjects.Add(new OneOnOneSubject(text, WorkDays.Today) { SharedOn = WorkDays.Today });
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
        var wanted = Objective.Subjects.Where(s => s.IsShared).OrderBy(s => s.SharedOn).ToList();
        if (!Shared.SequenceEqual(wanted))
        {
            Shared.Clear();
            foreach (var subject in wanted) Shared.Add(subject);
        }
        OnPropertyChanged(nameof(IsEmpty));
    }
}
