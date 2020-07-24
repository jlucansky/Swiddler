using Swiddler.SocketSettings;
using System.Windows.Controls;

namespace Swiddler.Views.SocketSettings
{
    /// <summary>
    /// Interaction logic for Rewrite.xaml
    /// </summary>
    public partial class Sniffer : UserControl
    {
        public Sniffer()
        {
            InitializeComponent();

            gridFilter.CellEditEnding += (s, e) => CaptureFilterChanged();
        }

        void CaptureFilterChanged()
        {
            var model = (SnifferSettings)DataContext;
            model.CaptureFilterChanges++; // this forces property change notification to make settings dirty
        }
    }
}
