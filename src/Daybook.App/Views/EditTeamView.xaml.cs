using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Daybook.App.Models;
using Daybook.App.ViewModels;

namespace Daybook.App.Views;

public partial class EditTeamView : UserControl
{
    public EditTeamView()
    {
        InitializeComponent();
        Loaded += (_, _) => AddBox.Focus();
        DataObject.AddPastingHandler(AddBox, OnPaste);
    }

    private EditTeamViewModel ViewModel => (EditTeamViewModel)DataContext;

    // Pasting a multi-line list adds one teammate per line.
    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !text.Contains('\n')) return;

        e.CancelCommand();
        ViewModel.AddMany(text.Split('\n'));
    }

    // Inline rename: Enter commits, Esc cancels, clicking away commits.
    private void Name_KeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        if (e.Key == Key.Escape) box.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
        else if (e.Key != Key.Enter) return;

        AddBox.Focus();
        e.Handled = true;
    }

    private void Name_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var box = (TextBox)sender;
        var binding = box.GetBindingExpression(TextBox.TextProperty)!;
        var name = box.Text.Trim();

        // Empty or duplicate name: quietly keep the old one.
        if (name.Length == 0 || ViewModel.Team.Contains(name, except: (Teammate)box.DataContext))
        {
            binding.UpdateTarget();
            return;
        }

        box.Text = name;
        binding.UpdateSource();
    }
}
