using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

public enum CheckupChoice { Clear, Keep, StillDoing, MarkDone, BackToPlanned, Start, Drop }

public sealed record CheckupOption(CheckupChoice Choice, string Label);

/// <summary>One task in a check-up step, with the choice pre-set to the usual answer.</summary>
public partial class CheckupItem(DayTask task, IReadOnlyList<CheckupOption> options) : ObservableObject
{
    public DayTask Task { get; } = task;
    public IReadOnlyList<CheckupOption> Options { get; } = options;

    [ObservableProperty]
    private CheckupChoice _choice = options[0].Choice;
}

/// <summary>
/// Morning check-up in three short steps: yesterday's done, doing, planned.
/// The defaults are right if you just press Continue.
/// </summary>
public partial class CheckupViewModel : ObservableObject
{
    private static readonly CheckupOption[] DoneOptions = [new(CheckupChoice.Clear, "Clear"), new(CheckupChoice.Keep, "Keep")];

    private static readonly CheckupOption[] DoingOptions =
    [
        new(CheckupChoice.StillDoing, "Still doing"), new(CheckupChoice.MarkDone, "Done"),
        new(CheckupChoice.BackToPlanned, "Back to planned"), new(CheckupChoice.Drop, "Drop"),
    ];

    private static readonly CheckupOption[] PlannedOptions =
        [new(CheckupChoice.Keep, "Keep"), new(CheckupChoice.Start, "Start now"), new(CheckupChoice.Drop, "Drop")];

    private readonly List<(string Title, string Hint, CheckupItem[] Items)> _steps;
    private readonly Action<IEnumerable<CheckupItem>> _finish;
    private readonly Action _later;
    private int _index;

    public CheckupViewModel(TaskList list, Action<IEnumerable<CheckupItem>> finish, Action later)
    {
        _finish = finish;
        _later = later;

        // Empty steps are skipped: no question without something to decide.
        _steps = new List<(string, string, CheckupItem[])>
        {
            ("Yesterday's done tasks", "Cleared from your list. 📣 ones stay in today's standup notes.",
                Pick(list, t => t.IsDone && t.DoneOn < WorkDays.Today, DoneOptions)),
            ("Still in progress", "Pick what happened to each one.",
                Pick(list, t => t.IsDoing, DoingOptions)),
            ("Still planned", "Keep for today, start, or let go.",
                Pick(list, t => t.IsPlanned, PlannedOptions)),
        }.Where(s => s.Item3.Length > 0).ToList();

        ShowStep();
    }

    public ObservableCollection<CheckupItem> Items { get; } = [];

    [ObservableProperty]
    private string _stepTitle = "";

    [ObservableProperty]
    private string _stepHint = "";

    [ObservableProperty]
    private string _continueLabel = "Continue";

    public bool IsEmpty => _steps.Count == 0;

    [RelayCommand]
    private void Continue()
    {
        if (++_index < _steps.Count) ShowStep();
        else _finish(_steps.SelectMany(s => s.Items));
    }

    [RelayCommand]
    private void Later() => _later();

    private void ShowStep()
    {
        if (IsEmpty) return;

        var (title, hint, items) = _steps[_index];
        StepTitle = $"Step {_index + 1} of {_steps.Count} · {title}";
        StepHint = hint;
        ContinueLabel = _index == _steps.Count - 1 ? "Finish" : "Continue";
        Items.Clear();
        foreach (var item in items) Items.Add(item);
    }

    private static CheckupItem[] Pick(TaskList list, Func<DayTask, bool> filter, CheckupOption[] options) =>
        list.Items.Where(filter).Select(t => new CheckupItem(t, options)).ToArray();
}
