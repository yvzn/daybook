using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Daybook.App.Models;

public partial class Team : ObservableObject
{
    [ObservableProperty]
    private int _meetingMinutes = 15;

    public ObservableCollection<Teammate> Members { get; } = [];

    public bool Contains(string name, Teammate? except = null) =>
        Members.Any(m => m != except && string.Equals(m.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase));

    partial void OnMeetingMinutesChanged(int value)
    {
        if (value < 1) MeetingMinutes = 1;
    }
}
