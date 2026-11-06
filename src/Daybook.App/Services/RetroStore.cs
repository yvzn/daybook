using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using Daybook.App.Models;

namespace Daybook.App.Services;

/// <summary>
/// Loads retro subjects from %AppData%\Daybook\retro.json and saves every change automatically (debounced).
/// </summary>
public sealed class RetroStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Daybook", "retro.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private RetroList? _list;

    public RetroStore() => _debounce.Tick += (_, _) => Save();

    public RetroList Load()
    {
        var list = new RetroList();
        try
        {
            if (File.Exists(FilePath) &&
                JsonSerializer.Deserialize<RetroData>(File.ReadAllText(FilePath), JsonOptions) is { } data)
            {
                foreach (var item in data.Items ?? [])
                    list.Items.Add(new RetroItem(item.Text, item.Kind, item.AddedOn) { IsDiscussed = item.IsDiscussed });
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable file: start with an empty board rather than crash.
        }

        list.Items.CollectionChanged += OnItemsChanged;
        foreach (var item in list.Items) item.PropertyChanged += OnChanged;
        _list = list;
        return list;
    }

    public void Flush()
    {
        if (_debounce.IsEnabled) Save();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (RetroItem item in e.OldItems ?? Array.Empty<RetroItem>()) item.PropertyChanged -= OnChanged;
        foreach (RetroItem item in e.NewItems ?? Array.Empty<RetroItem>()) item.PropertyChanged += OnChanged;
        ScheduleSave();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => ScheduleSave();

    private void ScheduleSave()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void Save()
    {
        _debounce.Stop();
        if (_list is null) return;

        var data = new RetroData(_list.Items.Select(i => new ItemData(i.Text, i.Kind, i.AddedOn, i.IsDiscussed)).ToList());
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    private sealed record RetroData(List<ItemData>? Items);

    private sealed record ItemData(string Text, RetroKind Kind, DateOnly AddedOn, bool IsDiscussed);
}
