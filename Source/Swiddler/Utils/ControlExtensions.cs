using Swiddler.Common;
using System;
using System.Windows.Controls.Primitives;

namespace Swiddler.Utils
{
    public static class ControlExtensions
    {
        public static void ReloadCertificateItems(this Selector control)
        {
            (control.ItemsSource as IHasReload).Reload();
            control.SelectedIndex = Math.Max(control.SelectedIndex, 0);
        }
    }
}
