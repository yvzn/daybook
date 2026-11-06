using System.Windows.Controls;
using System.Windows.Input;

namespace Daybook.App.Views;

public partial class EditObjectivesView : UserControl
{
    public EditObjectivesView()
    {
        InitializeComponent();
        Loaded += (_, _) => AddBox.Focus();
    }

    private void Text_KeyDown(object sender, KeyEventArgs e) => InlineEditing.KeyDown(sender, e);

    private void Text_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => InlineEditing.CommitRequired(sender, e);
}
