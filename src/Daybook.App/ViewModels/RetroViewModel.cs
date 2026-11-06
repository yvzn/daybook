using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>
/// Subjects for the next retro, in three columns. You pick the kind by typing in the right column: no picker.
/// </summary>
public partial class RetroViewModel : ObservableObject
{
    private readonly ToastViewModel _toast;

    public RetroViewModel(RetroList list, ToastViewModel toast)
    {
        List = list;
        _toast = toast;
        Columns =
        [
            new RetroColumnViewModel(this, RetroKind.WentWell, "Went well", "Something good…"),
            new RetroColumnViewModel(this, RetroKind.DidNotGoWell, "Didn't go well", "Something to fix…"),
            new RetroColumnViewModel(this, RetroKind.Idea, "Ideas", "An idea…"),
        ];
        List.Items.CollectionChanged += OnItemsChanged;
        foreach (var item in List.Items) item.PropertyChanged += OnItemChanged;
        Rebuild();
    }

    public RetroList List { get; }

    public IReadOnlyList<RetroColumnViewModel> Columns { get; }

    public int DiscussedCount => List.Items.Count(i => i.IsDiscussed);

    public bool HasDiscussed => DiscussedCount > 0;

    /// <summary>Says exactly what will happen, so no confirmation is needed (and there is undo).</summary>
    public string ClearLabel => $"Clear {DiscussedCount} discussed";

    internal void Add(RetroKind kind, string text) =>
        List.Items.Add(new RetroItem(text, kind, WorkDays.Today));

    [RelayCommand]
    private void Delete(RetroItem item)
    {
        var index = List.Items.IndexOf(item);
        if (index < 0) return;

        List.Items.RemoveAt(index);
        _toast.Show($"\"{item.Text}\" deleted", () => List.Items.Insert(Math.Min(index, List.Items.Count), item));
    }

    /// <summary>After the retro: talked-about subjects go, the rest wait for next time.</summary>
    [RelayCommand]
    private void ClearDiscussed()
    {
        var before = List.Items.ToArray();
        var cleared = before.Count(i => i.IsDiscussed);
        if (cleared == 0) return;

        foreach (var item in before.Where(i => i.IsDiscussed)) List.Items.Remove(item);
        _toast.Show(cleared == 1 ? "1 subject cleared" : $"{cleared} subjects cleared", () =>
        {
            List.Items.Clear();
            foreach (var item in before) List.Items.Add(item);
        });
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (RetroItem item in e.OldItems ?? Array.Empty<RetroItem>()) item.PropertyChanged -= OnItemChanged;
        foreach (RetroItem item in e.NewItems ?? Array.Empty<RetroItem>()) item.PropertyChanged += OnItemChanged;
        Rebuild();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RetroItem.IsDiscussed)) RaiseCounts();
    }

    private void Rebuild()
    {
        foreach (var column in Columns) column.Fill(List.Items);
        RaiseCounts();
    }

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(DiscussedCount));
        OnPropertyChanged(nameof(HasDiscussed));
        OnPropertyChanged(nameof(ClearLabel));
    }
}

/// <summary>One column: its own add box, its own items.</summary>
public partial class RetroColumnViewModel(RetroViewModel owner, RetroKind kind, string title, string placeholder)
    : ObservableObject
{
    public RetroKind Kind { get; } = kind;
    public string Title { get; } = title;
    public string Placeholder { get; } = placeholder;

    public ObservableCollection<RetroItem> Items { get; } = [];

    [ObservableProperty]
    private string _newText = "";

    [RelayCommand]
    private void Add()
    {
        var text = NewText.Trim();
        if (text.Length == 0) return;

        owner.Add(Kind, text);
        NewText = "";
    }

    internal void Fill(IEnumerable<RetroItem> all)
    {
        var wanted = all.Where(i => i.Kind == Kind).ToList();
        if (Items.SequenceEqual(wanted)) return;

        Items.Clear();
        foreach (var item in wanted) Items.Add(item);
    }
}
