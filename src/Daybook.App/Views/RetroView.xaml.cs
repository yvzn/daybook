using System.Windows.Controls;
using System.Windows.Input;

namespace Daybook.App.Views;

public partial class RetroView : UserControl
{
    public RetroView() => InitializeComponent();

    // Inline rename: Enter commits, Esc cancels, clicking away commits.
    private void Text_KeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        if (e.Key == Key.Escape) box.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
        else if (e.Key != Key.Enter) return;

        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void Text_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var box = (TextBox)sender;
        var binding = box.GetBindingExpression(TextBox.TextProperty)!;
        var text = box.Text.Trim();

        // Empty text: quietly keep the old one (delete is the ✕).
        if (text.Length == 0)
        {
            binding.UpdateTarget();
            return;
        }

        box.Text = text;
        binding.UpdateSource();
    }
}
