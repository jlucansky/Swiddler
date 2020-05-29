using System.Windows;
using System.Windows.Controls;

namespace Swiddler.Controls
{
    public class SearchTextBox : TextBox
    {
        static SearchTextBox()
        {
            // this is needed when style is defined in themes/generic.xaml
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SearchTextBox), new FrameworkPropertyMetadata(typeof(SearchTextBox)));
        }
    }
}
