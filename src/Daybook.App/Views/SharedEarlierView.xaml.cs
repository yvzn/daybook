using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Daybook.App.Models;
using Daybook.App.ViewModels;

namespace Daybook.App.Views;

public partial class SharedEarlierView : UserControl
{
    private OneOnOneSubject? _pickingDateFor;

    public SharedEarlierView() => InitializeComponent();

    private void Text_KeyDown(object sender, KeyEventArgs e) => InlineEditing.KeyDown(sender, e);

    private void Required_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => InlineEditing.CommitRequired(sender, e);

    private void Optional_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => InlineEditing.CommitOptional(sender, e);

    private void Date_Click(object sender, RoutedEventArgs e)
    {
        _pickingDateFor = (OneOnOneSubject)((FrameworkElement)sender).DataContext;
        var date = _pickingDateFor.Date.ToDateTime(TimeOnly.MinValue);
        DateCalendar.SelectedDate = null;
        DateCalendar.DisplayDate = date;
        DateCalendar.SelectedDate = date;
        DatePopup.IsOpen = true;
    }

    private void DateCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!DatePopup.IsOpen || DateCalendar.SelectedDate is not { } date || _pickingDateFor is null ||
            DataContext is not SharedEarlierViewModel vm) return;

        var day = DateOnly.FromDateTime(date);
        if (day == _pickingDateFor.Date) return; // the pre-selected current date

        DatePopup.IsOpen = false;
        vm.SetDate(_pickingDateFor, day);
        _pickingDateFor = null;
    }

    // WPF's Calendar keeps mouse capture after a click, which would swallow the next click elsewhere.
    private void DateCalendar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.Captured is CalendarItem) Mouse.Capture(null);
    }
}
