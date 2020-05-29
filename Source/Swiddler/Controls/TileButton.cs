using System.Windows;
using System.Windows.Controls;

namespace Swiddler.Controls
{
    public class TileButton : Button
    {
        static TileButton()
        {
            // this is needed when style is defined in themes/generic.xaml
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TileButton), new FrameworkPropertyMetadata(typeof(TileButton)));
        }
    }
}
