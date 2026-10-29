using CommunityToolkit.Mvvm.ComponentModel;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

public enum NoteKind { Done, Doing, Planned }

/// <summary>One line in the notes; the view picks the icon from <see cref="Kind"/>.</summary>
public sealed record NoteLine(string Text, NoteKind Kind);

/// <summary>"My standup notes": the shared tasks, as yesterday / today. Refreshed when the Standup tab opens.</summary>
public partial class StandupNotesViewModel(TaskList list) : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<NoteLine> _yesterday = [];

    [ObservableProperty]
    private IReadOnlyList<NoteLine> _today = [];

    public bool HasNotes => Yesterday.Count > 0 || Today.Count > 0;
    public bool HasYesterday => Yesterday.Count > 0;
    public bool HasToday => Today.Count > 0;

    public void Refresh()
    {
        var today = WorkDays.Today;
        var shared = list.Items.Where(t => t.IsShared).ToList();

        Yesterday =
        [
            .. (list.YesterdayFor == today ? list.Yesterday : []).Select(title => new NoteLine(title, NoteKind.Done)),
            .. shared.Where(t => t.IsDone && t.DoneOn < today).Select(t => new NoteLine(t.Title, NoteKind.Done)),
        ];
        Today =
        [
            .. shared.Where(t => t.IsDoing).Select(t => new NoteLine(t.Title, NoteKind.Doing)),
            .. shared.Where(t => t.IsDone && t.DoneOn == today).Select(t => new NoteLine(t.Title, NoteKind.Done)),
            .. shared.Where(t => t.IsPlanned).Select(t => new NoteLine(t.Title, NoteKind.Planned)),
        ];

        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasYesterday));
        OnPropertyChanged(nameof(HasToday));
    }
}
