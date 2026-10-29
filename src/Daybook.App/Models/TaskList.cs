using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Daybook.App.Models;

public partial class TaskList : ObservableObject
{
    public ObservableCollection<DayTask> Items { get; } = [];

    /// <summary>Day of the last morning check-up. Before today means one is due.</summary>
    [ObservableProperty]
    private DateOnly? _lastCheckup;

    /// <summary>Shared tasks finished yesterday and cleared at check-up, kept for today's standup notes.</summary>
    [ObservableProperty]
    private IReadOnlyList<string> _yesterday = [];

    [ObservableProperty]
    private DateOnly? _yesterdayFor;

    public bool IsCheckupDue => LastCheckup < WorkDays.Today && Items.Count > 0;
}
