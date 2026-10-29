using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using Daybook.App.Models;

namespace Daybook.App.Services;

/// <summary>
/// Loads the team from %AppData%\Daybook\team.json and saves every change automatically (debounced).
/// </summary>
public sealed class TeamStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Daybook", "team.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private Team? _team;

    public TeamStore() => _debounce.Tick += (_, _) => Save();

    public Team Load()
    {
        var team = new Team();
        try
        {
            if (File.Exists(FilePath) && JsonNode.Parse(File.ReadAllText(FilePath)) is { } root)
            {
                team.MeetingMinutes = root["MeetingMinutes"]?.GetValue<int>() ?? team.MeetingMinutes;
                if (root["Members"] is JsonArray members)
                    foreach (var node in members)
                    {
                        // Older files store plain names.
                        var member = node is JsonValue ? new MemberData(node.GetValue<string>()) : node?.Deserialize<MemberData>();
                        if (member is null) continue;

                        // Past "off until" dates are dropped: the person is back.
                        team.Members.Add(new Teammate(member.Name)
                        {
                            OffUntil = member.OffUntil >= WorkDays.Today ? member.OffUntil : null,
                        });
                    }
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException
                                      or InvalidOperationException or FormatException)
        {
            // Corrupt or unreadable file: start with an empty team rather than crash.
        }

        Watch(team);
        _team = team;
        return team;
    }

    public void Flush()
    {
        if (_debounce.IsEnabled) Save();
    }

    private void Watch(Team team)
    {
        team.PropertyChanged += OnChanged;
        team.Members.CollectionChanged += OnMembersChanged;
        foreach (var member in team.Members) member.PropertyChanged += OnChanged;
    }

    private void OnMembersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (Teammate member in e.OldItems ?? Array.Empty<Teammate>()) member.PropertyChanged -= OnChanged;
        foreach (Teammate member in e.NewItems ?? Array.Empty<Teammate>()) member.PropertyChanged += OnChanged;
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
        if (_team is null) return;

        var data = new TeamData(_team.MeetingMinutes, _team.Members.Select(m => new MemberData(m.Name, m.OffUntil)).ToList());
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOptions));
    }

    private sealed record TeamData(int MeetingMinutes, List<MemberData> Members);

    private sealed record MemberData(string Name, DateOnly? OffUntil = null);
}
