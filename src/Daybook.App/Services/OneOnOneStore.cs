using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using Daybook.App.Models;

namespace Daybook.App.Services;

/// <summary>
/// Loads objectives, subjects and issues from %AppData%\Daybook\oneonone.json and saves every change automatically
/// (debounced). The data is a small tree, so any change re-subscribes to the whole of it.
/// </summary>
public sealed class OneOnOneStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Daybook", "oneonone.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly List<INotifyPropertyChanged> _watchedObjects = [];
    private readonly List<INotifyCollectionChanged> _watchedCollections = [];
    private OneOnOneBook? _book;

    public OneOnOneStore() => _debounce.Tick += (_, _) => Save();

    public OneOnOneBook Load()
    {
        var book = new OneOnOneBook();
        try
        {
            if (File.Exists(FilePath) &&
                JsonSerializer.Deserialize<BookData>(File.ReadAllText(FilePath), JsonOptions) is { } data)
            {
                if (data.Current is not null) book.Current = FromData(data.Current);
                foreach (var semester in data.Past ?? []) book.Past.Add(FromData(semester));
                foreach (var issue in data.Issues ?? [])
                    book.Issues.Add(new Issue(issue.Text, issue.AddedOn) { IsTicked = issue.IsTicked, TimesRaised = issue.TimesRaised });
                foreach (var day in data.Meetings ?? []) book.Meetings.Add(day);
                book.IsInCall = data.InCall;
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable file: start empty rather than crash.
        }

        _book = book;
        Rewire();
        return book;
    }

    public void Flush()
    {
        if (_debounce.IsEnabled) Save();
    }

    private void Rewire()
    {
        foreach (var o in _watchedObjects) o.PropertyChanged -= OnPropertyChanged;
        foreach (var c in _watchedCollections) c.CollectionChanged -= OnCollectionChanged;
        _watchedObjects.Clear();
        _watchedCollections.Clear();
        if (_book is null) return;

        Watch(_book);
        WatchCollections(_book.Past, _book.Issues, _book.Meetings);
        foreach (var issue in _book.Issues) Watch(issue);
        foreach (var semester in _book.Past.Append(_book.Current))
        {
            Watch(semester);
            WatchCollections(semester.Objectives);
            foreach (var objective in semester.AllGroups)
            {
                Watch(objective);
                WatchCollections(objective.Subjects);
                foreach (var subject in objective.Subjects) Watch(subject);
            }
        }
    }

    private void Watch(INotifyPropertyChanged o)
    {
        o.PropertyChanged += OnPropertyChanged;
        _watchedObjects.Add(o);
    }

    private void WatchCollections(params INotifyCollectionChanged[] collections)
    {
        foreach (var c in collections)
        {
            c.CollectionChanged += OnCollectionChanged;
            _watchedCollections.Add(c);
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is OneOnOneBook && e.PropertyName == nameof(OneOnOneBook.Current)) Rewire();
        ScheduleSave();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Rewire();
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void Save()
    {
        _debounce.Stop();
        if (_book is null) return;

        var data = new BookData(
            ToData(_book.Current),
            _book.Past.Select(ToData).ToList(),
            _book.Issues.Select(i => new IssueData(i.Text, i.AddedOn, i.IsTicked, i.TimesRaised)).ToList(),
            _book.Meetings.ToList(),
            _book.IsInCall);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    private static SemesterData ToData(Semester s) =>
        new(s.Label, s.StartedOn, s.Objectives.Select(ToData).ToList(), ToData(s.Other));

    private static ObjectiveData ToData(Objective o) => new(o.Title, o.AddedOn,
        o.Subjects.Select(x => new SubjectData(x.Text, x.AddedOn, x.IsTicked, x.SharedOn, x.Outcome)).ToList());

    private static Objective FromData(ObjectiveData o, string? title = null)
    {
        var objective = new Objective(title ?? o.Title, o.AddedOn);
        foreach (var s in o.Subjects ?? [])
            objective.Subjects.Add(new OneOnOneSubject(s.Text, s.AddedOn)
                { IsTicked = s.IsTicked, SharedOn = s.SharedOn, Outcome = s.Outcome });
        return objective;
    }

    private static Semester FromData(SemesterData data)
    {
        var semester = data.Other is { } other
            ? new Semester(data.Label, data.StartedOn) { Other = FromData(other, Semester.OtherTitle) }
            : new Semester(data.Label, data.StartedOn);
        foreach (var o in data.Objectives ?? []) semester.Objectives.Add(FromData(o));
        return semester;
    }

    private sealed record BookData(SemesterData? Current, List<SemesterData>? Past, List<IssueData>? Issues, List<DateOnly>? Meetings, bool InCall = false);

    private sealed record SemesterData(string Label, DateOnly StartedOn, List<ObjectiveData>? Objectives, ObjectiveData? Other);

    private sealed record ObjectiveData(string Title, DateOnly AddedOn, List<SubjectData>? Subjects);

    private sealed record SubjectData(string Text, DateOnly AddedOn, bool IsTicked, DateOnly? SharedOn, string? Outcome);

    private sealed record IssueData(string Text, DateOnly AddedOn, bool IsTicked, int TimesRaised);
}
