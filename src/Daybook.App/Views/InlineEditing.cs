using System.Windows.Controls;
using System.Windows.Input;

namespace Daybook.App.Views;

/// <summary>
/// Inline edit for text bound with UpdateSourceTrigger=Explicit: Enter commits, Esc cancels, clicking away commits.
/// </summary>
internal static class InlineEditing
{
    public static void KeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        if (e.Key == Key.Escape) box.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
        else if (e.Key != Key.Enter) return;

        Keyboard.ClearFocus();
        e.Handled = true;
    }

    /// <summary>Required text: emptying it quietly keeps the old one (delete is the ✕).</summary>
    public static void CommitRequired(object sender, KeyboardFocusChangedEventArgs e) => Commit((TextBox)sender, allowEmpty: false);

    /// <summary>Optional text (outcome): emptying it clears it.</summary>
    public static void CommitOptional(object sender, KeyboardFocusChangedEventArgs e) => Commit((TextBox)sender, allowEmpty: true);

    private static void Commit(TextBox box, bool allowEmpty)
    {
        var binding = box.GetBindingExpression(TextBox.TextProperty)!;
        var text = box.Text.Trim();

        if (text.Length == 0 && !allowEmpty)
        {
            binding.UpdateTarget();
            return;
        }

        box.Text = text;
        binding.UpdateSource();
    }
}
