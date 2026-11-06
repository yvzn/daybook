using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daybook.App.Models;

namespace Daybook.App.ViewModels;

/// <summary>Objectives for the current semester. Like Edit team: no Save button, every change applies at once.</summary>
public partial class EditObjectivesViewModel(OneOnOneBook book, ToastViewModel toast, Action back) : ObservableObject
{
    public OneOnOneBook Book { get; } = book;

    /// <summary>Re-read on every visit: "Start new semester" replaces it.</summary>
    public Semester Semester => Book.Current;

    [ObservableProperty]
    private string _newTitle = "";

    public void Refresh() => OnPropertyChanged(nameof(Semester));

    [RelayCommand]
    private void Add()
    {
        var title = NewTitle.Trim();
        if (title.Length == 0) return;

        Semester.Objectives.Add(new Objective(title, WorkDays.Today));
        NewTitle = "";
    }

    /// <summary>Its subjects go with it; undo brings everything back.</summary>
    [RelayCommand]
    private void Remove(Objective objective)
    {
        var objectives = Semester.Objectives;
        var index = objectives.IndexOf(objective);
        if (index < 0) return;

        objectives.RemoveAt(index);
        toast.Show($"\"{objective.Title}\" removed", () => objectives.Insert(Math.Min(index, objectives.Count), objective));
    }

    [RelayCommand]
    private void Back() => back();
}
