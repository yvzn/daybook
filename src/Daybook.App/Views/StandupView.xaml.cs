using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Daybook.App.ViewModels;

namespace Daybook.App.Views;

public partial class StandupView : UserControl
{
    private Window? _window;

    public StandupView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window is not null) _window.PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window is not null) _window.PreviewKeyDown -= OnPreviewKeyDown;
        _window = null;
    }

    // Space = Next, wherever focus is (handled before a focused button can swallow it).
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || OffDatePopup.IsOpen || Keyboard.FocusedElement is TextBox
            || DataContext is not StandupViewModel vm) return;

        vm.NextCommand.Execute(null);
        e.Handled = true;
    }

    private SpeakerViewModel? _pickingOffFor;

    private void PickOffDate_Click(object sender, RoutedEventArgs e)
    {
        _pickingOffFor = (SpeakerViewModel)((FrameworkElement)sender).DataContext;
        OffCalendar.DisplayDateStart = DateTime.Today;
        OffCalendar.DisplayDate = DateTime.Today;
        OffCalendar.SelectedDate = null;
        OffDatePopup.IsOpen = true;
    }

    private void OffCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (OffCalendar.SelectedDate is not { } date || _pickingOffFor is null || DataContext is not StandupViewModel vm) return;

        OffDatePopup.IsOpen = false;
        vm.MarkOff(_pickingOffFor, DateOnly.FromDateTime(date));
        _pickingOffFor = null;
    }

    // WPF's Calendar keeps mouse capture after a click, which would swallow the next click elsewhere.
    private void OffCalendar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.Captured is System.Windows.Controls.Primitives.CalendarItem) Mouse.Capture(null);
    }
}
