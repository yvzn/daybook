using CommunityToolkit.Mvvm.ComponentModel;

namespace Daybook.App.Models;

public partial class Teammate(string name) : ObservableObject
{
    [ObservableProperty]
    private string _name = name;

    /// <summary>Last day off (vacation, sick leave…), inclusive. Null when available. The reason doesn't matter.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOff), nameof(OffLabel), nameof(BackLabel))]
    private DateOnly? _offUntil;

    public bool IsOff => OffUntil >= WorkDays.Today;

    public string OffLabel => IsOff ? $"Off until {WorkDays.Short(OffUntil!.Value)}" : "";

    public string BackLabel => IsOff ? $"back {WorkDays.Short(WorkDays.NextWorkingDay(OffUntil!.Value))}" : "";
}
