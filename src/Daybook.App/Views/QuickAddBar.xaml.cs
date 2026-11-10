using System.Windows;
using System.Windows.Controls;

namespace Daybook.App.Views;

/// <summary>Bottom add box of the 1:1 pages. See <see cref="ViewModels.QuickAddViewModel"/>.</summary>
public partial class QuickAddBar : UserControl
{
    public QuickAddBar() => InitializeComponent();

    /// <summary>Put the cursor in the box, e.g. when the page opens.</summary>
    public void FocusBox()
    {
        Box.Focus();
        Box.CaretIndex = Box.Text.Length;
    }

    /// <summary>Back to objectives (from Issue) and show the list: one click either way.</summary>
    private void ObjectiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.QuickAddViewModel { IsIssue: true, Objective: { } current } vm)
            vm.PickObjectiveCommand.Execute(current);
        Picker.IsOpen = !Picker.IsOpen;
    }

    private void PickerItem_Click(object sender, RoutedEventArgs e)
    {
        Picker.IsOpen = false;
        FocusBox();
    }

    private void IssueButton_Click(object sender, RoutedEventArgs e) => FocusBox();
}
