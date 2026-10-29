using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>
/// Edit team. No Save button: every change applies (and is stored) immediately.
/// </summary>
public partial class EditTeamViewModel(Team team, ToastViewModel toast, Action showStandup) : ObservableObject
{
    public Team Team { get; } = team;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicateHint))]
    private string _newName = "";

    public string? DuplicateHint =>
        !string.IsNullOrWhiteSpace(NewName) && Team.Contains(NewName) ? $"{NewName.Trim()} already in team" : null;

    [RelayCommand]
    private void Add()
    {
        if (TryAdd(NewName)) NewName = "";
    }

    /// <summary>Pasting several lines adds one teammate per line.</summary>
    public void AddMany(IEnumerable<string> names)
    {
        var added = names.Count(TryAdd);
        if (added > 0) toast.Show(added == 1 ? "1 teammate added" : $"{added} teammates added");
    }

    [RelayCommand]
    private void Remove(Teammate teammate)
    {
        var index = Team.Members.IndexOf(teammate);
        if (index < 0) return;

        Team.Members.RemoveAt(index);
        toast.Show($"{teammate.Name} removed",
            () => Team.Members.Insert(Math.Min(index, Team.Members.Count), teammate));
    }

    [RelayCommand]
    private void ClearOff(Teammate teammate)
    {
        var previous = teammate.OffUntil;
        teammate.OffUntil = null;
        toast.Show($"{teammate.Name} is back", () => teammate.OffUntil = previous);
    }

    [RelayCommand]
    private void Back() => showStandup();

    private bool TryAdd(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || Team.Contains(name)) return false;

        Team.Members.Add(new Teammate(name));
        return true;
    }
}
