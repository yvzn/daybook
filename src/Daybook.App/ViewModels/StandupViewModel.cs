using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>
/// Run mode. Clicking a name means "they are talking now"; the previous speaker is marked done.
/// </summary>
public partial class StandupViewModel : ObservableObject
{
    private readonly Team _team;
    private readonly ToastViewModel _toast;
    private readonly Action _showEditTeam;
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromMilliseconds(250) };

    // Meeting time = offset + running stopwatch, so undo can restore a consistent clock.
    private TimeSpan _clockOffset;
    private TimeSpan _turnStartedAt;
    private TimeSpan _elapsedBeforeTurn;

    public ObservableCollection<SpeakerViewModel> Waiting { get; } = [];
    public ObservableCollection<SpeakerViewModel> Done { get; } = [];
    public ObservableCollection<SpeakerViewModel> Away { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrent))]
    private SpeakerViewModel? _current;

    [ObservableProperty]
    private string _meetingClock = "";

    [ObservableProperty]
    private bool _isMeetingOver;

    public bool HasCurrent => Current is not null;
    public bool HasTeam => _team.Members.Count > 0;
    public bool HasWaiting => Waiting.Count > 0;
    public bool HasDone => Done.Count > 0;
    public bool HasAway => Away.Count > 0;
    public bool IsFinished => Current is null && Waiting.Count == 0 && Done.Count > 0;

    private TimeSpan Now => _clockOffset + _clock.Elapsed;
    private TimeSpan MeetingLength => TimeSpan.FromMinutes(_team.MeetingMinutes);

    private IEnumerable<SpeakerViewModel> AllSpeakers =>
        Waiting.Concat(Done).Concat(Away).Concat(Current is null ? [] : [Current]);

    public StandupViewModel(Team team, ToastViewModel toast, Action showEditTeam)
    {
        _team = team;
        _toast = toast;
        _showEditTeam = showEditTeam;
        _ticker.Tick += (_, _) => Tick();
        StartFresh();
    }

    [RelayCommand]
    private void Talk(SpeakerViewModel speaker)
    {
        if (speaker == Current) return;

        var undo = TakeSnapshot();
        var previous = Current;
        FinishCurrent();
        Waiting.Remove(speaker);
        Done.Remove(speaker);
        Away.Remove(speaker);
        BeginTurn(speaker);
        Refresh();

        _toast.Show(previous is null
            ? $"{speaker.Teammate.Name} is talking"
            : $"{previous.Teammate.Name} done · {speaker.Teammate.Name} talking", undo);
    }

    [RelayCommand]
    private void Next()
    {
        if (Waiting.Count > 0)
        {
            Talk(Waiting[0]);
            return;
        }

        if (Current is null) return;

        var undo = TakeSnapshot();
        FinishCurrent();
        _clockOffset = Now;
        _clock.Reset();
        _ticker.Stop();
        Refresh();
        _toast.Show("Everyone has spoken", undo);
    }

    [RelayCommand]
    private void MarkAway(SpeakerViewModel speaker)
    {
        var undo = TakeSnapshot();
        speaker.IsAwayToday = true;
        MoveToAway(speaker);
        _toast.Show($"{speaker.Teammate.Name} away today", undo);
    }

    // Context-menu labels carry the date, so "tomorrow" on a Friday visibly means Monday.
    public string OffTomorrowLabel => $"Off until tomorrow ({WorkDays.Short(WorkDays.Tomorrow)})";
    public string OffEndOfWeekLabel => $"Off until end of week ({WorkDays.Short(WorkDays.EndOfWeek)})";
    public string OffEndOfNextWeekLabel => $"Off until end of next week ({WorkDays.Short(WorkDays.EndOfNextWeek)})";

    [RelayCommand]
    private void OffTomorrow(SpeakerViewModel speaker) => MarkOff(speaker, WorkDays.Tomorrow);

    [RelayCommand]
    private void OffEndOfWeek(SpeakerViewModel speaker) => MarkOff(speaker, WorkDays.EndOfWeek);

    [RelayCommand]
    private void OffEndOfNextWeek(SpeakerViewModel speaker) => MarkOff(speaker, WorkDays.EndOfNextWeek);

    /// <summary>Off for several days (vacation, sick leave…): stored, and back automatically after <paramref name="lastDayOff"/>.</summary>
    public void MarkOff(SpeakerViewModel speaker, DateOnly lastDayOff)
    {
        var restore = TakeSnapshot();
        var previous = speaker.Teammate.OffUntil;
        speaker.Teammate.OffUntil = lastDayOff;
        speaker.IsAwayToday = false;
        MoveToAway(speaker);
        _toast.Show($"{speaker.Teammate.Name} off until {WorkDays.Short(lastDayOff)}", () =>
        {
            speaker.Teammate.OffUntil = previous;
            restore();
        });
    }

    /// <summary>Back today: also ends an "off until" early.</summary>
    [RelayCommand]
    private void MarkBack(SpeakerViewModel speaker)
    {
        var restore = TakeSnapshot();
        var previous = speaker.Teammate.OffUntil;
        speaker.Teammate.OffUntil = null;
        speaker.IsAwayToday = false;
        Away.Remove(speaker);
        speaker.State = SpeakerState.Waiting;
        Waiting.Add(speaker);
        Refresh();
        _toast.Show($"{speaker.Teammate.Name} is back", () =>
        {
            speaker.Teammate.OffUntil = previous;
            restore();
        });
    }

    private void MoveToAway(SpeakerViewModel speaker)
    {
        if (speaker == Current) Current = null;
        Waiting.Remove(speaker);
        Done.Remove(speaker);
        speaker.State = SpeakerState.Away;
        if (!Away.Contains(speaker)) Away.Add(speaker);
        Refresh();
    }

    [RelayCommand]
    private void Shuffle()
    {
        var shuffled = Waiting.OrderBy(_ => Random.Shared.Next()).ToList();
        Waiting.Clear();
        foreach (var speaker in shuffled) Waiting.Add(speaker);
    }

    [RelayCommand]
    private void Reset()
    {
        var undo = TakeSnapshot();
        StartFresh();
        _toast.Show("Standup reset", undo);
    }

    [RelayCommand]
    private void EditTeam() => _showEditTeam();

    /// <summary>
    /// Picks up changes from the edit screen: teammates added, removed or renamed, "off until" cleared,
    /// and a new meeting length.
    /// </summary>
    public void SyncWithTeam()
    {
        var members = _team.Members.ToHashSet();
        foreach (var list in new[] { Waiting, Done, Away })
            foreach (var gone in list.Where(s => !members.Contains(s.Teammate)).ToList())
                list.Remove(gone);
        if (Current is not null && !members.Contains(Current.Teammate)) Current = null;

        var known = AllSpeakers.Select(s => s.Teammate).ToHashSet();
        foreach (var member in _team.Members.Where(m => !known.Contains(m)))
            Waiting.Add(new SpeakerViewModel(member));

        foreach (var speaker in Waiting.Where(s => s.Teammate.IsOff).ToList())
        {
            Waiting.Remove(speaker);
            speaker.State = SpeakerState.Away;
            Away.Add(speaker);
        }
        foreach (var speaker in Away.Where(s => !s.Teammate.IsOff && !s.IsAwayToday).ToList())
        {
            Away.Remove(speaker);
            speaker.State = SpeakerState.Waiting;
            Waiting.Add(speaker);
        }

        Refresh();
    }

    private void StartFresh()
    {
        _ticker.Stop();
        _clock.Reset();
        _clockOffset = TimeSpan.Zero;
        Current = null;
        Waiting.Clear();
        Done.Clear();
        Away.Clear();
        foreach (var member in _team.Members.OrderBy(_ => Random.Shared.Next()))
        {
            var speaker = new SpeakerViewModel(member);
            if (member.IsOff)
            {
                speaker.State = SpeakerState.Away;
                Away.Add(speaker);
            }
            else Waiting.Add(speaker);
        }
        Refresh();
    }

    private void BeginTurn(SpeakerViewModel speaker)
    {
        _elapsedBeforeTurn = speaker.Elapsed; // non-zero when someone talks again
        _turnStartedAt = Now;
        speaker.State = SpeakerState.Talking;
        Current = speaker;
        _clock.Start();
        _ticker.Start();
    }

    private void FinishCurrent()
    {
        if (Current is null) return;
        Tick();
        Current.State = SpeakerState.Done;
        Done.Add(Current);
        Current = null;
    }

    private void Tick()
    {
        if (Current is not null) Current.Elapsed = _elapsedBeforeTurn + (Now - _turnStartedAt);
        UpdateClock();
    }

    private void Refresh()
    {
        var present = Waiting.Count + Done.Count + (Current is null ? 0 : 1);
        var budget = present > 0 ? MeetingLength / present : MeetingLength;
        foreach (var speaker in AllSpeakers) speaker.Budget = budget;

        UpdateClock();
        OnPropertyChanged(nameof(HasTeam));
        OnPropertyChanged(nameof(HasWaiting));
        OnPropertyChanged(nameof(HasDone));
        OnPropertyChanged(nameof(HasAway));
        OnPropertyChanged(nameof(IsFinished));
    }

    private void UpdateClock()
    {
        var left = MeetingLength - Now;
        IsMeetingOver = left < TimeSpan.Zero;
        MeetingClock = IsMeetingOver
            ? $"+{SpeakerViewModel.Format(-left)} over"
            : $"{SpeakerViewModel.Format(left)} left";
    }

    private Action TakeSnapshot()
    {
        var snapshot = new Snapshot(
            Current,
            [.. Waiting],
            [.. Done],
            [.. Away],
            AllSpeakers.Select(s => (s, s.State, s.Elapsed, s.IsAwayToday)).ToArray(),
            Now,
            _clock.IsRunning,
            _turnStartedAt,
            _elapsedBeforeTurn,
            Stopwatch.GetTimestamp());
        return () => Restore(snapshot);
    }

    private void Restore(Snapshot snapshot)
    {
        // Time that passed since the snapshot is real meeting time, so the clock keeps it.
        var since = snapshot.ClockRunning ? Stopwatch.GetElapsedTime(snapshot.TakenAt) : TimeSpan.Zero;
        _clock.Reset();
        _clockOffset = snapshot.Now + since;
        if (snapshot.ClockRunning) _clock.Start();
        _turnStartedAt = snapshot.TurnStartedAt;
        _elapsedBeforeTurn = snapshot.ElapsedBeforeTurn;

        foreach (var (speaker, state, elapsed, isAwayToday) in snapshot.Speakers)
        {
            speaker.State = state;
            speaker.Elapsed = elapsed;
            speaker.IsAwayToday = isAwayToday;
        }

        Fill(Waiting, snapshot.Waiting);
        Fill(Done, snapshot.Done);
        Fill(Away, snapshot.Away);
        Current = snapshot.Current;

        if (snapshot.ClockRunning) _ticker.Start(); else _ticker.Stop();
        SyncWithTeam(); // team may have changed meanwhile
        Tick();
    }

    private static void Fill(ObservableCollection<SpeakerViewModel> target, SpeakerViewModel[] items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    private sealed record Snapshot(
        SpeakerViewModel? Current,
        SpeakerViewModel[] Waiting,
        SpeakerViewModel[] Done,
        SpeakerViewModel[] Away,
        (SpeakerViewModel Speaker, SpeakerState State, TimeSpan Elapsed, bool IsAwayToday)[] Speakers,
        TimeSpan Now,
        bool ClockRunning,
        TimeSpan TurnStartedAt,
        TimeSpan ElapsedBeforeTurn,
        long TakenAt);
}
