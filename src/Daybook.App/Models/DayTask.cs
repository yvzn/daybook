using CommunityToolkit.Mvvm.ComponentModel;

namespace Daybook.App.Models;

public enum DayTaskStatus { Planned, Doing, Done }

public partial class DayTask(string title) : ObservableObject
{
    [ObservableProperty]
    private string _title = title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone), nameof(IsDoing), nameof(IsPlanned))]
    private DayTaskStatus _status;

    /// <summary>Worth mentioning at standup.</summary>
    [ObservableProperty]
    private bool _isShared;

    /// <summary>Day it was finished: tells "done yesterday" from "done today".</summary>
    [ObservableProperty]
    private DateOnly? _doneOn;

    public bool IsDoing => Status == DayTaskStatus.Doing;
    public bool IsPlanned => Status == DayTaskStatus.Planned;

    /// <summary>Checkbox binding: ticking marks done, unticking puts it back in Planned.</summary>
    public bool IsDone
    {
        get => Status == DayTaskStatus.Done;
        set => Status = value ? DayTaskStatus.Done : DayTaskStatus.Planned;
    }

    partial void OnStatusChanged(DayTaskStatus value) =>
        DoneOn = value == DayTaskStatus.Done ? DoneOn ?? WorkDays.Today : null;
}
