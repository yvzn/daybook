using CommunityToolkit.Mvvm.ComponentModel;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>"My standup notes": the 📣 tasks, as yesterday / today. Refreshed when the Standup tab opens.</summary>
public partial class StandupNotesViewModel(TaskList list) : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<string> _yesterday = [];

    [ObservableProperty]
    private IReadOnlyList<string> _today = [];

    public bool HasNotes => Yesterday.Count > 0 || Today.Count > 0;
    public bool HasYesterday => Yesterday.Count > 0;
    public bool HasToday => Today.Count > 0;

    public void Refresh()
    {
        var today = WorkDays.Today;
        var shared = list.Items.Where(t => t.IsShared).ToList();

        Yesterday =
        [
            .. list.YesterdayFor == today ? list.Yesterday : [],
            .. shared.Where(t => t.IsDone && t.DoneOn < today).Select(t => t.Title),
        ];
        Today =
        [
            .. shared.Where(t => t.IsDoing).Select(t => $"▶ {t.Title}"),
            .. shared.Where(t => t.IsDone && t.DoneOn == today).Select(t => $"✓ {t.Title}"),
            .. shared.Where(t => t.IsPlanned).Select(t => t.Title),
        ];

        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasYesterday));
        OnPropertyChanged(nameof(HasToday));
    }
}
