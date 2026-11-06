using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Daybook.App.Models;
using Daybook.App.ViewModels;

namespace Daybook.App.Views;

public partial class TodayView : UserControl
{
    public TodayView()
    {
        InitializeComponent();
        Loaded += (_, _) => AddBox.Focus();
    }

    // Inline rename: Enter commits, Esc cancels, clicking away commits.
    private void Title_KeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        if (e.Key == Key.Escape) box.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
        else if (e.Key != Key.Enter) return;

        AddBox.Focus();
        e.Handled = true;
    }

    private void Title_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var box = (TextBox)sender;
        var binding = box.GetBindingExpression(TextBox.TextProperty)!;
        var title = box.Text.Trim();

        // Empty title: quietly keep the old one (delete is the ✕).
        if (title.Length == 0)
        {
            binding.UpdateTarget();
            return;
        }

        box.Text = title;
        binding.UpdateSource();
    }

    // "→ 1:1": a short menu of this semester's objectives, "Outside objectives", then "Issues & asks". Built on click, so it is current.
    private void SendToOneOnOne_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TodayViewModel vm) return;
        var button = (Button)sender;
        var task = (DayTask)button.DataContext;

        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        menu.Items.Add(new MenuItem { Header = "Send to 1:1", IsEnabled = false });
        foreach (var objective in vm.OneOnOneObjectives)
            menu.Items.Add(Item(objective.Title, () => vm.SendToOneOnOne(task, objective)));
        if (vm.OneOnOneObjectives.Count == 0)
            menu.Items.Add(new MenuItem { Header = "No objectives yet (1:1 tab › Edit)", IsEnabled = false });
        menu.Items.Add(Item(vm.OneOnOneOther.Title, () => vm.SendToOneOnOne(task, vm.OneOnOneOther)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Issues & asks", () => vm.SendToOneOnOne(task, null)));
        menu.IsOpen = true;

        static MenuItem Item(string header, Action send)
        {
            // Header as a TextBlock: an objective title with "_" must not become an access key.
            var item = new MenuItem { Header = new TextBlock { Text = header } };
            item.Click += (_, _) => send();
            return item;
        }
    }
}
