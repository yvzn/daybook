using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Daybook.App.Models;

/// <summary>Something to tell the manager about one objective. Once shared, it is kept for the semester review.</summary>
public partial class OneOnOneSubject(string text, DateOnly addedOn) : ObservableObject
{
    [ObservableProperty]
    private string _text = text;

    /// <summary>Ticked during the meeting; "Done with 1:1" turns ticks into <see cref="SharedOn"/>.</summary>
    [ObservableProperty]
    private bool _isTicked;

    /// <summary>Day of the 1:1 it was shared at. Null: still to share.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShared), nameof(Date), nameof(DateText))]
    private DateOnly? _sharedOn;

    /// <summary>What the manager said: "OK for ES rollout". Optional.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutcome))]
    private string? _outcome;

    public DateOnly AddedOn { get; } = addedOn;

    public bool HasOutcome => !string.IsNullOrEmpty(Outcome);

    public bool IsShared => SharedOn is not null;

    /// <summary>Shared: the 1:1 date. Still to share: the day it was noted.</summary>
    public DateOnly Date => SharedOn ?? AddedOn;

    public string DateText => OneOnOneBook.Format(Date);
}

/// <summary>One personal objective for the semester: a title, a reminder, never a form.</summary>
public partial class Objective(string title, DateOnly addedOn) : ObservableObject
{
    [ObservableProperty]
    private string _title = title;

    public DateOnly AddedOn { get; } = addedOn;

    public ObservableCollection<OneOnOneSubject> Subjects { get; } = [];
}

/// <summary>Something to escalate or ask. Gone once ticked and the 1:1 is done.</summary>
public partial class Issue(string text, DateOnly addedOn) : ObservableObject
{
    [ObservableProperty]
    private string _text = text;

    [ObservableProperty]
    private bool _isTicked;

    /// <summary>How many 1:1s it survived without being ticked: an escalation that keeps coming back.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RaisedText), nameof(WasRaised))]
    private int _timesRaised;

    public DateOnly AddedOn { get; } = addedOn;

    public string AddedText => OneOnOneBook.Format(AddedOn);

    public bool WasRaised => TimesRaised > 0;

    public string RaisedText => $"Raised {TimesRaised}×";
}

/// <summary>A semester runs from one "Start new semester" to the next: no fixed dates, meetings slip.</summary>
public partial class Semester(string label, DateOnly startedOn) : ObservableObject
{
    [ObservableProperty]
    private string _label = label;

    public DateOnly StartedOn { get; } = startedOn;

    public ObservableCollection<Objective> Objectives { get; } = [];

    public const string OtherTitle = "Outside objectives";

    /// <summary>
    /// Progress that fits no objective but still counts at review time. Always last; can't be renamed or removed,
    /// so it lives apart from <see cref="Objectives"/>.
    /// </summary>
    public Objective Other { get; init; } = new(OtherTitle, startedOn);

    /// <summary>Objectives, then "Outside objectives": every place a subject can be.</summary>
    public IEnumerable<Objective> AllGroups => Objectives.Append(Other);
}

public partial class OneOnOneBook : ObservableObject
{
    /// <summary>A semester is "due" for review after this long, around mid-June or mid-December.</summary>
    private static readonly int ReviewAfterDays = 90;

    [ObservableProperty]
    private Semester _current = new(LabelFor(WorkDays.Today), WorkDays.Today);

    /// <summary>Newest first.</summary>
    public ObservableCollection<Semester> Past { get; } = [];

    public ObservableCollection<Issue> Issues { get; } = [];

    /// <summary>"Start 1:1" was pressed and "Done with 1:1" not yet: the page shows the call view, even after a restart.</summary>
    [ObservableProperty]
    private bool _isInCall;

    /// <summary>Days a 1:1 was done: drives "Not mentioned in last 3 1:1s".</summary>
    public ObservableCollection<DateOnly> Meetings { get; } = [];

    /// <summary>
    /// "S2 2026". Looks a month ahead: a semester started on 20 Dec, right after the review, is next year's S1.
    /// </summary>
    public static string LabelFor(DateOnly start)
    {
        var date = start.AddDays(30);
        return $"S{(date.Month <= 6 ? 1 : 2)} {date.Year}";
    }

    /// <summary>Label for a semester starting today after <paramref name="previous"/>: never the same label twice.</summary>
    public static string LabelAfter(Semester previous, DateOnly start)
    {
        var label = LabelFor(start);
        if (label != previous.Label) return label;

        // Started early (review held in November): it is still the next semester.
        var parts = label.Split(' ');
        return parts is ["S1", var year] ? $"S2 {year}"
             : parts is ["S2", var y] && int.TryParse(y, out var n) ? $"S1 {n + 1}"
             : label;
    }

    public static string Format(DateOnly day) => day.ToString("ddd d MMM", CultureInfo.InvariantCulture);

    /// <summary>From mid-June / mid-December, once the semester has run long enough to deserve a review.</summary>
    public bool IsReviewSeason
    {
        get
        {
            var today = WorkDays.Today;
            var inSeason = today is { Month: 6, Day: >= 15 } or { Month: 7 }
                                 or { Month: 12, Day: >= 15 } or { Month: 1 };
            return inSeason && today.DayNumber - Current.StartedOn.DayNumber >= ReviewAfterDays;
        }
    }

    /// <summary>1:1s done since the objective was last mentioned (or created).</summary>
    public int MeetingsWithout(Objective objective)
    {
        var last = objective.Subjects.Select(s => s.SharedOn).Max() ?? objective.AddedOn;
        return Meetings.Count(m => m > last);
    }
}
