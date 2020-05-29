using Swiddler.ChunkViews;
using Swiddler.Common;
using Swiddler.Utils;
using System;
using System.Windows;
using System.Windows.Media;

namespace Swiddler.Rendering
{
    /// <summary>
    /// Visual representation of the chunk on layer.
    /// </summary>
    public abstract class Fragment : BindableBase
    {
        /// <summary>
        /// Owner of the fragment
        /// </summary>
        public FragmentLayer Layer { get; internal set; }

        public FragmentView View => Layer.Owner;

        public Visual Visual { get; private set; } // null when fragment is outside viewport

        public abstract Type VisualType { get; }

        public VisualPool VisualPool { get; private set; }

        public ChunkViewItem Source { get; set; }

        public Rect Bounds { get; set; }

        public long ApproxFileOffset { get; set; } // approx position of fragment in file
        public int ApproxFileLength { get; set; } // approx length of fragment in file

        public bool ShouldVisualise => Visual == null || _needVisualiseFlag;

        public Action MouseEnter { get; set; }
        public Action MouseLeave { get; set; }

        public bool IsMouseHover { get; set; }
        public bool IsSelectable { get; set; }

        public Fragment PreviousSelectable { get; set; }
        public FragmentRef NextSelectable { get; set; }

        public double DpiScale => App.Current.Res.DpiScale;
        public double OneByDpiScale => 1 / DpiScale;

        public bool IsTrailing { get; set; } // fragment is last from actual chunk

        bool _needVisualiseFlag;

        private void CreateVisual()
        {
            // najde/vytvori VisualPool pre VisualType, ziska visual z poolu, nastavi this.Visual

            if (VisualPool == null)
                VisualPool = View.GetVisualPool(VisualType);

            Visual = VisualPool.GetFresh();

            Layer.AddVisual(Visual);
            Layer.VisibleFragments.Add(this);

            if (Visual is UIElement)
                _needVisualiseFlag = true; // Adding UIElement to ContainerVisual during Arrange phase triggers second Arrange later.
        }

        public void Visualise()
        {
            _needVisualiseFlag = false;

            if (Visual == null)
                CreateVisual();

            Refresh();
        }

        public void Recycle()
        {
            _needVisualiseFlag = false;

            if (Visual == null)
                return;

            if (IsMouseHover)
            {
                MouseLeave?.Invoke();
                IsMouseHover = false;
                _needVisualiseFlag = false;
                Layer.MouseHoverFragments.Remove(this);
            }

            Layer.RemoveVisual(Visual);
            Layer.VisibleFragments.Remove(this);

            Visual.SetDataContext(null);

            VisualPool.Recycle(Visual);

            Visual = null;
        }

        protected virtual void Refresh()
        {
            // nastavi fragment (this) do Visual.DataContext ak Visual dedi od FrameworkElement
            Visual.SetDataContext(this);

            if (Visual is UIElement element)
            {
                if (Visual is FrameworkElement frameworkElement) // properly size UserControls
                {
                    frameworkElement.Width = Bounds.Width;
                    frameworkElement.Height = Bounds.Height;
                }

                element.Arrange(Bounds);
            }
        }

        public void Invalidate(bool invalidateParent = false)
        {
            _needVisualiseFlag = true;

            if (invalidateParent)
                View.InvalidateArrange();
        }

        public bool Contains(Point point)
        {
            return Bounds.Contains(point) && HitTest(point);
        }

        protected virtual bool HitTest(Point point) => true; // by default only check if point is inside Bounds
    }

    [System.Diagnostics.DebuggerDisplay("{Fragment}")]
    public class FragmentRef { public Fragment Fragment; }
         
}
