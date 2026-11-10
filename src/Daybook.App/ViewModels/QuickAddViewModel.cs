using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>
/// The add box at the bottom of the 1:1 pages: pick where it goes (an objective, or issues), type, Enter.
/// The choice sticks, so adding several things to the same place is just typing.
/// </summary>
public partial class QuickAddViewModel : ObservableObject
{
    private readonly Func<IEnumerable<Objective>> _objectives;
    private readonly Action<Objective, string>? _addSubject;
    private readonly Action<string>? _addIssue;
    private const int ShortTitleWords = 4;

    private readonly string _verb;

    /// <param name="objectives">Where a subject can go; re-read each time, objectives change.</param>
    /// <param name="addSubject">Null: no objectives here (call view, issues only).</param>
    /// <param name="addIssue">Null: no issues here (semester review).</param>
    public QuickAddViewModel(Func<IEnumerable<Objective>> objectives, Action<Objective, string>? addSubject,
        Action<string>? addIssue, string verb = "Add a subject to")
    {
        _objectives = objectives;
        _addSubject = addSubject;
        _addIssue = addIssue;
        _verb = verb;
        _isIssue = addSubject is null;
        Sync();
    }

    /// <summary>The objective picker shows: subjects can be added here.</summary>
    public bool CanPickObjective => _addSubject is not null;

    /// <summary>The "Issue" choice shows: both kinds can be added here.</summary>
    public bool CanPickIssue => _addSubject is not null && _addIssue is not null;

    public IReadOnlyList<Objective> Objectives => _objectives().ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetLabel), nameof(IsObjectiveSelected))]
    private bool _isIssue;

    /// <summary>Where the next subject goes, when <see cref="IsIssue"/> is off.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetLabel))]
    private Objective? _objective;

    public bool IsObjectiveSelected => !IsIssue;

    /// <summary>Placeholder, first part: "Add a subject to".</summary>
    public string Verb => _verb;

    /// <summary>Placeholder, after the ⚑ or ! icon: where it goes, objectives cut to their first words.</summary>
    public string TargetLabel => IsIssue ? "Issues & asks" : ShortTitle(Objective?.Title ?? "");

    private static string ShortTitle(string title)
    {
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= ShortTitleWords ? title : string.Join(' ', words.Take(ShortTitleWords)) + "…";
    }

    [ObservableProperty]
    private string _text = "";

    /// <summary>Objectives changed (edited, new semester): keep the choice if it still exists.</summary>
    public void Sync()
    {
        var objectives = Objectives;
        if (Objective is null || !objectives.Contains(Objective)) Objective = objectives.FirstOrDefault();
        OnPropertyChanged(nameof(Objectives));
    }

    [RelayCommand]
    private void PickObjective(Objective objective)
    {
        Objective = objective;
        IsIssue = false;
    }

    [RelayCommand]
    private void PickIssue() => IsIssue = true;

    [RelayCommand]
    private void Add()
    {
        var text = Text.Trim();
        if (text.Length == 0) return;

        if (IsIssue) _addIssue?.Invoke(text);
        else if (Objective is not null) _addSubject?.Invoke(Objective, text);
        else return;
        Text = "";
    }
}
