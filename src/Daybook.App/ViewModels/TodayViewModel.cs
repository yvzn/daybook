using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>
/// Today's tasks in three groups (Doing, Planned, Done), plus the morning check-up.
/// </summary>
public partial class TodayViewModel : ObservableObject
{
    private const int FocusLimit = 3;

    private readonly ToastViewModel _toast;

    public TodayViewModel(TaskList list, ToastViewModel toast)
    {
        List = list;
        _toast = toast;
        List.Items.CollectionChanged += OnItemsChanged;
        foreach (var task in List.Items) task.PropertyChanged += OnTaskChanged;
        Rebuild();
    }

    public TaskList List { get; }

    public ObservableCollection<DayTask> Doing { get; } = [];
    public ObservableCollection<DayTask> Planned { get; } = [];
    public ObservableCollection<DayTask> Done { get; } = [];

    public string DateText => DateTime.Today.ToString("ddd d MMM", CultureInfo.InvariantCulture);

    [ObservableProperty]
    private string _newTitle = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCheckupOpen), nameof(ShowBanner), nameof(ShowList))]
    private CheckupViewModel? _checkup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBanner))]
    private bool _bannerDismissed;

    public bool IsCheckupOpen => Checkup is not null;
    public bool ShowList => !IsCheckupOpen;
    public bool ShowBanner => List.IsCheckupDue && !IsCheckupOpen && !BannerDismissed;

    public bool HasDoing => Doing.Count > 0;
    public bool HasPlanned => Planned.Count > 0;
    public bool HasDone => Done.Count > 0;
    public bool IsEmpty => List.Items.Count == 0;

    /// <summary>A nudge, not a block.</summary>
    public string? FocusHint => Doing.Count > FocusLimit ? $"{Doing.Count} in progress. Focus?" : null;

    [RelayCommand]
    private void Add()
    {
        var title = NewTitle.Trim();
        if (title.Length == 0) return;

        List.Items.Add(new DayTask(title));
        NewTitle = "";
    }

    [RelayCommand]
    private void Start(DayTask task) => task.Status = DayTaskStatus.Doing;

    [RelayCommand]
    private void Stop(DayTask task) => task.Status = DayTaskStatus.Planned;

    [RelayCommand]
    private void Delete(DayTask task)
    {
        var index = List.Items.IndexOf(task);
        if (index < 0) return;

        List.Items.RemoveAt(index);
        _toast.Show($"\"{task.Title}\" deleted", () => List.Items.Insert(Math.Min(index, List.Items.Count), task));
    }

    [RelayCommand]
    private void StartCheckup()
    {
        var checkup = new CheckupViewModel(List, FinishCheckup, () => { Checkup = null; BannerDismissed = true; });
        if (checkup.IsEmpty) FinishCheckup([]);
        else Checkup = checkup;
    }

    [RelayCommand]
    private void DismissBanner() => BannerDismissed = true;

    private void FinishCheckup(IEnumerable<CheckupItem> items)
    {
        var undo = TakeSnapshot();
        var yesterday = new List<string>();

        foreach (var item in items)
        {
            var task = item.Task;
            switch (item.Choice)
            {
                case CheckupChoice.Clear:
                case CheckupChoice.MarkDone: // finished yesterday: belongs to yesterday's notes
                    if (task.IsShared) yesterday.Add(task.Title);
                    List.Items.Remove(task);
                    break;
                case CheckupChoice.BackToPlanned:
                    task.Status = DayTaskStatus.Planned;
                    break;
                case CheckupChoice.Start:
                    task.Status = DayTaskStatus.Doing;
                    break;
                case CheckupChoice.Drop:
                    List.Items.Remove(task);
                    break;
            }
        }

        List.Yesterday = yesterday;
        List.YesterdayFor = WorkDays.Today;
        List.LastCheckup = WorkDays.Today;
        Checkup = null;
        OnPropertyChanged(nameof(ShowBanner));
        _toast.Show("Day ready", undo);
    }

    private Action TakeSnapshot()
    {
        var tasks = List.Items.Select(t => (Task: t, t.Status, t.DoneOn)).ToArray();
        var (lastCheckup, yesterday, yesterdayFor) = (List.LastCheckup, List.Yesterday, List.YesterdayFor);
        return () =>
        {
            List.Items.Clear();
            foreach (var (task, status, doneOn) in tasks)
            {
                task.Status = status;
                task.DoneOn = doneOn;
                List.Items.Add(task);
            }
            (List.LastCheckup, List.Yesterday, List.YesterdayFor) = (lastCheckup, yesterday, yesterdayFor);
            BannerDismissed = false;
            OnPropertyChanged(nameof(ShowBanner));
        };
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (DayTask task in e.OldItems ?? Array.Empty<DayTask>()) task.PropertyChanged -= OnTaskChanged;
        foreach (DayTask task in e.NewItems ?? Array.Empty<DayTask>()) task.PropertyChanged += OnTaskChanged;
        Rebuild();
    }

    private void OnTaskChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DayTask.Status)) Rebuild();
    }

    // Groups follow the list's order; Doing first because it's what matters now.
    private void Rebuild()
    {
        Fill(Doing, DayTaskStatus.Doing);
        Fill(Planned, DayTaskStatus.Planned);
        Fill(Done, DayTaskStatus.Done);

        OnPropertyChanged(nameof(HasDoing));
        OnPropertyChanged(nameof(HasPlanned));
        OnPropertyChanged(nameof(HasDone));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(FocusHint));
        OnPropertyChanged(nameof(ShowBanner));
    }

    private void Fill(ObservableCollection<DayTask> group, DayTaskStatus status)
    {
        var wanted = List.Items.Where(t => t.Status == status).ToList();
        if (group.SequenceEqual(wanted)) return;

        group.Clear();
        foreach (var task in wanted) group.Add(task);
    }
}
