using CommunityToolkit.Mvvm.ComponentModel;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

public enum SpeakerState { Waiting, Talking, Done, Away }

public enum TimeLevel { Normal, Warning, Over }

public partial class SpeakerViewModel : ObservableObject
{
    public SpeakerViewModel(Teammate teammate)
    {
        Teammate = teammate;
        Teammate.PropertyChanged += (_, _) => OnPropertyChanged(nameof(AwayText));
    }

    public Teammate Teammate { get; }

    [ObservableProperty]
    private SpeakerState _state;

    /// <summary>Marked away for this standup only (as opposed to off for several days).</summary>
    [ObservableProperty]
    private bool _isAwayToday;

    public string AwayText => Teammate.IsOff ? $"{Teammate.Name} · {Teammate.BackLabel}" : Teammate.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BarValue), nameof(Level), nameof(ElapsedText), nameof(TimeText))]
    private TimeSpan _elapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BarValue), nameof(Level), nameof(ElapsedText), nameof(TimeText))]
    private TimeSpan _budget;

    public TimeLevel Level =>
        Elapsed > Budget ? TimeLevel.Over
        : Elapsed >= Budget * 0.75 ? TimeLevel.Warning
        : TimeLevel.Normal;

    /// <summary>Fraction of the budget still left: the bar shrinks, then turns full red once over.</summary>
    public double BarValue =>
        Level == TimeLevel.Over || Budget <= TimeSpan.Zero ? 1 : 1 - Elapsed / Budget;

    public string ElapsedText => Format(Elapsed);

    public string TimeText =>
        Elapsed <= Budget ? $"{Format(Elapsed)} / {Format(Budget)}" : $"+{Format(Elapsed - Budget)} over";

    internal static string Format(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";
}
