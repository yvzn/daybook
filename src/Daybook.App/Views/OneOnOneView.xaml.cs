using System.Windows.Controls;
using System.Windows.Input;

namespace Daybook.App.Views;

public partial class OneOnOneView : UserControl
{
    public OneOnOneView() => InitializeComponent();

    private void Text_KeyDown(object sender, KeyEventArgs e) => InlineEditing.KeyDown(sender, e);

    private void Text_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => InlineEditing.CommitRequired(sender, e);
}
