using System.Collections.Specialized;
using System.Windows.Controls;

namespace Swiddler.Controls
{
    public class ComboBoxEx : ComboBox
    {
        private bool ignore = false;
        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            if (!ignore)
            {
                base.OnSelectionChanged(e);
            }
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            if (IsEditable)
                ignore = true;

            try
            {
                base.OnItemsChanged(e);
                
                if (IsEditable)
                {
                    var oldText = Text;
                    Text = null;
                    Text = oldText; // forces to update selection
                }
            }
            finally
            {
                ignore = false;
            }
        }


    }
}
