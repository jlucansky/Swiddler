using System.Windows;
using System.Windows.Media;

namespace Swiddler.Utils
{
    public static class VisualExtensions
    {
        internal static void SetDataContext(this Visual visual, object dataContext)
        {
            var element = visual as FrameworkElement;
            if (element != null)
                element.DataContext = dataContext;
        }
    }
}
