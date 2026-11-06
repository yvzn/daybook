using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Daybook.App.Models;

/// <summary>The three columns every retro board already uses.</summary>
public enum RetroKind { WentWell, DidNotGoWell, Idea }

/// <summary>One subject to bring up at the next retro.</summary>
public partial class RetroItem(string text, RetroKind kind, DateOnly addedOn) : ObservableObject
{
    [ObservableProperty]
    private string _text = text;

    /// <summary>Talked about during the retro: dimmed, cleared with "Clear discussed".</summary>
    [ObservableProperty]
    private bool _isDiscussed;

    public RetroKind Kind { get; } = kind;

    /// <summary>When it was noted: weeks later, the date brings back the context.</summary>
    public DateOnly AddedOn { get; } = addedOn;

    public string AddedText => AddedOn.ToString("ddd d MMM", CultureInfo.InvariantCulture);
}

public class RetroList
{
    public ObservableCollection<RetroItem> Items { get; } = [];
}
