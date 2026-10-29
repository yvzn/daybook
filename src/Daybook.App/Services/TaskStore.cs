using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using Daybook.App.Models;

namespace Daybook.App.Services;

/// <summary>
/// Loads today's tasks from %AppData%\Daybook\tasks.json and saves every change automatically (debounced).
/// </summary>
public sealed class TaskStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Daybook", "tasks.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private TaskList? _list;

    public TaskStore() => _debounce.Tick += (_, _) => Save();

    public TaskList Load()
    {
        var list = new TaskList();
        try
        {
            if (File.Exists(FilePath) &&
                JsonSerializer.Deserialize<TaskListData>(File.ReadAllText(FilePath), JsonOptions) is { } data)
            {
                list.LastCheckup = data.LastCheckup;
                list.Yesterday = data.Yesterday ?? [];
                list.YesterdayFor = data.YesterdayFor;
                foreach (var task in data.Tasks ?? [])
                    list.Items.Add(new DayTask(task.Title) { Status = task.Status, IsShared = task.IsShared, DoneOn = task.DoneOn });
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable file: start with an empty list rather than crash.
        }

        // Nothing to review yet: no check-up today.
        if (list.LastCheckup is null || list.Items.Count == 0) list.LastCheckup = WorkDays.Today;

        Watch(list);
        _list = list;
        return list;
    }

    public void Flush()
    {
        if (_debounce.IsEnabled) Save();
    }

    private void Watch(TaskList list)
    {
        list.PropertyChanged += OnChanged;
        list.Items.CollectionChanged += OnItemsChanged;
        foreach (var task in list.Items) task.PropertyChanged += OnChanged;
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (DayTask task in e.OldItems ?? Array.Empty<DayTask>()) task.PropertyChanged -= OnChanged;
        foreach (DayTask task in e.NewItems ?? Array.Empty<DayTask>()) task.PropertyChanged += OnChanged;
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

        var data = new TaskListData(
            _list.LastCheckup,
            _list.YesterdayFor,
            [.. _list.Yesterday],
            _list.Items.Select(t => new TaskData(t.Title, t.Status, t.IsShared, t.DoneOn)).ToList());
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    private sealed record TaskListData(DateOnly? LastCheckup, DateOnly? YesterdayFor, List<string>? Yesterday, List<TaskData>? Tasks);

    private sealed record TaskData(string Title, DayTaskStatus Status, bool IsShared, DateOnly? DoneOn);
}
