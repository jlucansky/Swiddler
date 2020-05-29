using Swiddler.ChunkViews;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Swiddler.Rendering
{
    [DebuggerDisplay("Count = {Items.Count}")]
    public class FragmentLayer
    {
        public FragmentView Owner { get; }
        public ContainerVisual Container { get; }

        public List<Fragment> Items { get; } = new List<Fragment>();
        public HashSet<Fragment> VisibleFragments { get; } = new HashSet<Fragment>();
        public HashSet<Fragment> MouseHoverFragments { get; } = new HashSet<Fragment>();

        public double Height { get; set; }

        private List<int> SpatialIndex { get; } = new List<int>(); // layer is divided to multiple spatial blocks and SpatialIndex contains index to the latest Item existing in this block
        private const double SpatialBlockHeight = 50; // spatial block height in points

        private FragmentRef nextSelectable = null;
        private Fragment previousSelectable = null;

        public FragmentLayer(FragmentView owner)
        {
            Owner = owner;
            Container = new ContainerVisual() { Transform = owner.ScrollTransform };
        }

        public void Add(Fragment fragment)
        {
            fragment.Layer = this;
            AddSpatialIndex(fragment, Items.Count);
            Items.Add(fragment);
            Height = Math.Max(Height, fragment.Bounds.Bottom);

            if (fragment.IsSelectable)
            {
                if (nextSelectable != null)
                {
                    nextSelectable.Fragment = fragment;
                    nextSelectable = null;
                }

                previousSelectable = fragment;
            }
            else
            {
                if (nextSelectable == null)
                    nextSelectable = new FragmentRef();
                fragment.PreviousSelectable = previousSelectable;
                fragment.NextSelectable = nextSelectable;
            }
        }

        void AddSpatialIndex(Fragment fragment, int itemIndex)
        {
            var bounds = fragment.Bounds;

            var indexTop = (int)(bounds.Top / SpatialBlockHeight);
            var indexBottom = (int)(bounds.Bottom / SpatialBlockHeight);

            for (int i = indexTop; i <= indexBottom; i++)
            {
                while (SpatialIndex.Count <= i) SpatialIndex.Add(itemIndex);
                SpatialIndex[i] = itemIndex;
            }
        }

        protected Tuple<int, int> FindVisibleRange(double offset, double height)
        {
            var result = Tuple.Create(
                BinarySearch(offset, BeginingFragmentComparer.Default),
                BinarySearch(offset + height, EndingFragmentComparer.Default));
            return result.Item1 != -1 && result.Item2 != -1 ? result : null;
        }

        public int FindFragmentAtOffset(double offset)
        {
            return BinarySearch(offset, EndingFragmentComparer.Default);
        }

        public int Find(int start, int count, Func<Fragment, int> binarySearchComparer)
        {
            var comparer = CustomComparer.Default;
            comparer.Comparer = binarySearchComparer;
            return ~Items.BinarySearch(start, count, null, comparer);
        }

        public int FindInRange(int start, int end, Func<Fragment, int> binarySearchComparer)
        {
            return Find(start, end - start + 1, binarySearchComparer);
        }

        public virtual void ArrangeViewport()
        {
            var canHover = Owner.Mouse.IsOverView;
            var mousePos = Owner.Mouse.Position;
            var visibleFragmentsInterval = FindVisibleRange(-Owner.ScrollTransform.Y, Owner.Content.Metrics.Viewport.Height);
            var fragmentsToHide = new HashSet<Fragment>(VisibleFragments);

            if (visibleFragmentsInterval != null)
            {
                for (int i = visibleFragmentsInterval.Item1; i <= visibleFragmentsInterval.Item2; i++)
                {
                    var fragment = Items[i];

                    fragmentsToHide.Remove(fragment);

                    if (fragment.ShouldVisualise) fragment.Visualise();

                    if (canHover) TestMouseHover(fragment, mousePos);
                }
            }

            foreach (var fragment in fragmentsToHide)
                fragment.Recycle();
        }

        public void AddVisual(Visual visual)
        {
            Container.Children.Add(visual);
        }

        public void RemoveVisual(Visual visual)
        {
            Container.Children.Remove(visual);
        }

        public virtual void Clear()
        {
            Container.Children.Clear();

            foreach (var item in Items) item.Recycle();

            SpatialIndex.Clear();
            Items.Clear();
            Height = 0;

            nextSelectable = null;
            previousSelectable = null;
        }

        public virtual void MouseChanged(MouseState mouse)
        {
            if (Items.Count == 0) return;

            var pos = mouse.Position;

            int spatialBlockIndex = Math.Min(SpatialIndex.Count - 1, (int)(pos.Y / SpatialBlockHeight));
            int firstFragment = 0, lastFragment = Items.Count - 1;

            if (spatialBlockIndex >= 0)
            {
                lastFragment = SpatialIndex[spatialBlockIndex];
                if (spatialBlockIndex > 0)
                    firstFragment = SpatialIndex[spatialBlockIndex - 1];
            }

            var selectionLayer = Owner.SelectionLayer;
            var oldSelection = new { selectionLayer.SelectionStart, selectionLayer.SelectionEnd };

            bool selected = false;
            Fragment hitFragment = null, firstLineHit = null, lastLineHit = null;
            for (int i = firstFragment; i <= lastFragment; i++)
            {
                var fragment = Items[i];
                if (fragment.Contains(pos))
                {
                    hitFragment = fragment;

                    if (mouse.IsOverView)
                    {
                        TrySetMouseHover(fragment);
                    }
                    if (mouse.IsSelecting)
                    {
                        selected = selectionLayer.TrySetSelection(fragment);
                    }

                    break;
                }
                else if (lastLineHit == null)
                {
                    var bounds = fragment.Bounds;
                    if (pos.Y >= bounds.Top && pos.Y <= bounds.Bottom)
                    {
                        if (firstLineHit == null)
                            firstLineHit = fragment;
                        lastLineHit = fragment;
                    }
                }
            }

            if (mouse.IsSelecting && !selected)
            {
                if (hitFragment != null) // mouse is inside of some fragment
                {
                    // find nearest selectable fragment
                    selectionLayer.SetNearestSelection(hitFragment, pos);
                }
                else
                {
                    if (firstLineHit == null)
                    {
                        firstLineHit = Items.FirstOrDefault();
                        lastLineHit = Items.LastOrDefault();
                    }

                    if (firstLineHit != null) // mouse is outside of any fragment
                    {
                        // find first or last selectable fragment
                        var firstBounds = firstLineHit.Bounds;
                        var lastBounds = lastLineHit.Bounds;
                        if (pos.Y < firstBounds.Top || (pos.X < firstBounds.Left && pos.Y >= firstBounds.Top && pos.Y <= firstBounds.Bottom))
                            selectionLayer.SetNearestSelection(firstLineHit, pos);
                        else if (pos.Y > lastBounds.Bottom || (pos.X > lastBounds.Right && pos.Y >= lastBounds.Top && pos.Y <= lastBounds.Bottom))
                            selectionLayer.SetNearestSelection(lastLineHit, pos);
                    }
                }
            }

            LeaveUnhoveredFragments(mouse);

            if (mouse.IsSelecting && !oldSelection.Equals(new { selectionLayer.SelectionStart, selectionLayer.SelectionEnd }))
                Owner.InvalidateArrange();
        }

        protected void LeaveUnhoveredFragments(MouseState mouse)
        {
            var pos = mouse.Position;

            List<Fragment> leavedFragments = null;
            foreach (var fragment in MouseHoverFragments)
            {
                if (!mouse.IsOverView || !fragment.Contains(pos))
                {
                    if (fragment.IsMouseHover)
                    {
                        fragment.MouseLeave?.Invoke();
                        fragment.IsMouseHover = false;
                    }

                    if (leavedFragments == null) leavedFragments = new List<Fragment>();
                    leavedFragments.Add(fragment);
                }
            }

            leavedFragments?.ForEach(f => MouseHoverFragments.Remove(f));
        }

        protected bool TrySetMouseHover(Fragment fragment)
        {
            // set mouse hover when fragment supports it
            if (!fragment.IsMouseHover && fragment.MouseEnter != null)
            {
                SetMouseHoverCore(fragment);
                return true;
            }
            return false;
        }

        private void TestMouseHover(Fragment fragment, Point point)
        {
            // fragment.Contains(point) can be slow so first check if fragment supports mouse hover
            if (!fragment.IsMouseHover && fragment.MouseEnter != null && fragment.Contains(point))
                SetMouseHoverCore(fragment);
        }

        private void SetMouseHoverCore(Fragment fragment)
        {
            fragment.IsMouseHover = true;
            fragment.MouseEnter();
            MouseHoverFragments.Add(fragment);
        }

        int BinarySearch(double offset, IOffsetComparer comparer)
        {
            comparer.Offset = offset;
            int spatialBlockIndex = Math.Min(SpatialIndex.Count - 1, (int)(offset / SpatialBlockHeight));
            int firstFragment = 0, lastFragment = Items.Count - 1;

            if (spatialBlockIndex >= 0)
            {
                lastFragment = SpatialIndex[spatialBlockIndex];
                if (spatialBlockIndex > 0)
                    firstFragment = SpatialIndex[spatialBlockIndex - 1];
            }

            return Math.Min(Items.Count - 1, ~Items.BinarySearch(firstFragment, lastFragment - firstFragment + 1, null, comparer));
        }

        private interface IOffsetComparer : IComparer<Fragment> { double Offset { get; set; } }

        private class BeginingFragmentComparer : IOffsetComparer
        {
            [ThreadStatic] public static readonly BeginingFragmentComparer Default = new BeginingFragmentComparer();
            public double Offset { get; set; }
            public int Compare(Fragment fragment, Fragment _) => fragment.Bounds.Bottom < Offset ? -1 : 1;
        }

        private class EndingFragmentComparer : IOffsetComparer
        {
            [ThreadStatic] public static readonly EndingFragmentComparer Default = new EndingFragmentComparer();
            public double Offset { get; set; }
            public int Compare(Fragment fragment, Fragment _) => fragment.Bounds.Y < Offset ? -1 : 1;
        }

        private class CustomComparer : IComparer<Fragment>
        {
            [ThreadStatic] public static readonly CustomComparer Default = new CustomComparer();
            public Func<Fragment, int> Comparer { get; set; }
            public int Compare(Fragment fragment, Fragment _) => Comparer(fragment);
        }

    }

}
