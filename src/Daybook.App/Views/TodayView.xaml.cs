using System.Windows.Controls;
using System.Windows.Input;

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
}
