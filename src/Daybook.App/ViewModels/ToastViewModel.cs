using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Daybook.App.ViewModels;

/// <summary>
/// "Bob removed · Undo" bar. Replaces confirmation dialogs: act first, offer undo for a few seconds.
/// </summary>
public partial class ToastViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private Action? _undo;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _canUndo;

    public ToastViewModel() => _timer.Tick += (_, _) => Hide();

    public void Show(string message, Action? undo = null)
    {
        Message = message;
        _undo = undo;
        CanUndo = undo is not null;
        IsVisible = true;
        _timer.Stop();
        _timer.Start();
    }

    [RelayCommand]
    private void Undo()
    {
        var undo = _undo;
        Hide();
        undo?.Invoke();
    }

    private void Hide()
    {
        _timer.Stop();
        _undo = null;
        IsVisible = false;
    }
}
