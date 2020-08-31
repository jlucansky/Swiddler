using Swiddler.ChunkViews;
using Swiddler.DataChunks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace Swiddler.Rendering
{
    public class SelectionLayer : FragmentLayer
    {
        public FilledFragment HighlightFragment { get; } = new FilledFragment(); // highlight drawing
        public FilledFragment SelectionFragment { get; } = new FilledFragment();
        public Fragment CurrentlySelectedFragment { get; set; } // actual selected fragment
        public Fragment CurrentlyHighlightedFragment { get; set; } // actual highlighted fragment


        public SelectionAnchorCaret SelectionStart { get; private set; }
        public SelectionAnchorCaret SelectionEnd { get; private set; }

        readonly List<Fragment> fragments = new List<Fragment>();

        public SelectionLayer(FragmentView owner) : base(owner)
        {
            fragments.Add(HighlightFragment);
            fragments.Add(SelectionFragment);

            foreach (var f in fragments) f.Layer = this;

            if (App.Current != null) // design mode
            {
                SelectionFragment.Brush = App.Current.Res.SelectionBrush;
                SelectionFragment.Pen = App.Current.Res.SelectionPen;
            }

            SelectionFragment.MouseEnter = () => SelectionMouseOver(true);
            SelectionFragment.MouseLeave = () => SelectionMouseOver(false);
        }

        private void SelectionMouseOver(bool hovered)
        {
            Owner.Mouse.IsOverSelection = hovered;
            Owner.UpdateCursor();
        }

        public override void ArrangeViewport()
        {
            fragments.ForEach(x => x.Visualise());
        }

        public override void Clear()
        {
            base.Clear();

            CurrentlySelectedFragment?.MouseLeave();
            CurrentlyHighlightedFragment?.MouseLeave();

            foreach (var f in fragments) f.Recycle();

            ResetSelection();
        }

        public void ResetSelection()
        {
            SelectionStart = null;
            SelectionEnd = null;

            UpdateSelectionPolygon();
        }

        public void SetSelection(SelectionAnchorCaret start, SelectionAnchorCaret end)
        {
            if (start != null)
            {
                SelectionStart = start;
                Owner.Content.SelectionStart = start.Anchor;
            }

            SelectionEnd = end;
            Owner.Content.SelectionEnd = end.Anchor;

            UpdateSelectionPolygon();
        }

        public override void MouseChanged(MouseState mouse)
        {
            if (!mouse.IsCaptured && SelectionFragment.Contains(mouse.Position))
            {
                TrySetMouseHover(SelectionFragment);
            }

            LeaveUnhoveredFragments(mouse);
        }

        void UpdateSelectionPolygon()
        {
            SelectionFragment.Polgyons = CreateSelectionPolygons(SelectionStart, SelectionEnd, out var bounds);
            if (SelectionFragment.Polgyons != null)
                SelectionFragment.Bounds = bounds;
        }

        PolygonFigure[] CreateSelectionPolygons(SelectionAnchorCaret start, SelectionAnchorCaret end, out Rect bounds)
        {
            bounds = default;

            if (start == null || end == null) return null;
            if (start.Anchor.Equals(end.Anchor)) return null; 

            if (start.Anchor.CompareTo(end.Anchor) > 0) // swap when start > end
            {
                var _ = start;
                start = end;
                end = _;
            }

            var content = Owner.Content;
            return FilledFragment.CreatePolygons(
                start.Bounds.TopLeft, end.Bounds.TopLeft, content.LineHeight,
                content.SnapToPixelsX(content.Metrics.Viewport.Width - content.DpiScale.X), out bounds);
        }

        public void SetSelection(SelectionAnchorCaret caret)
        {
            var content = Owner.Content;
            if (content.SelectionStart == null)
                SetSelection(caret, caret);
            else
                SetSelection(null, caret);
        }

        public bool TrySetSelection(Fragment fragment)
        {
            if (fragment.IsSelectable)
            {
                var caret = new SelectionAnchorCaret() { Fragment = fragment };
                fragment.Source.PrepareSelectionAnchor(caret);
                SetSelection(caret);

                if (Owner.Mouse.SelectWholeChunks)
                {
                    SelectionStart = null;
                    SelectionEnd = null;

                    var content = Owner.Content;

                    if (content.SelectionStart.CompareTo(content.SelectionEnd) > 0)
                    {
                        if (Owner.Content.SelectionStart.Chunk is Packet startPacket)
                        {
                            content.SelectionStart = new SelectionAnchor() { Chunk = content.SelectionStart.Chunk, Offset = startPacket.Payload.Length }; ;
                            content.SelectionEnd.Offset = 0;
                        }
                    }
                    else
                    {
                        if (Owner.Content.SelectionEnd.Chunk is Packet endPacket)
                        {
                            content.SelectionStart.Offset = 0;
                            content.SelectionEnd = new SelectionAnchor() { Chunk = content.SelectionEnd.Chunk, Offset = endPacket.Payload.Length };
                        }
                    }

                    AdjustSelectionAnchors();
                }

                return true;
            }
            return false;
        }

        public void SetNearestSelection(Fragment fragment, Point mousePosition)
        {
            if (fragment.IsSelectable)
            {
                TrySetSelection(fragment);
                return;
            }

            var prev = fragment.PreviousSelectable;
            var next = fragment.NextSelectable?.Fragment;

            if (prev == null && next == null) return;

            double prevDist = double.MaxValue, nextDist = double.MaxValue;

            if (prev != null) prevDist = GetPointDistance(prev.Bounds, mousePosition);
            if (next != null) nextDist = GetPointDistance(next.Bounds, mousePosition);

            TrySetSelection(prevDist < nextDist ? prev : next);
        }

        private double GetPointDistance(Rect bounds, Point point)
        {
            if (point.Y > bounds.Bottom)
                return point.Y - bounds.Bottom;
            else if (point.Y < bounds.Top)
                return bounds.Top - point.Y;
            else
            {
                if (point.X > bounds.Right)
                    return point.X - bounds.Right;
                else if (point.X < bounds.Left)
                    return bounds.Left - point.X;
                else
                    return 0; // point is inside box
            }
        }

        public void AdjustSelectionAnchors()
        {
            bool updated = false;

            SelectionStart = UpdateCaret(Owner.Content.SelectionStart, SelectionStart, ref updated);
            SelectionEnd = UpdateCaret(Owner.Content.SelectionEnd, SelectionEnd, ref updated);

            if (updated) UpdateSelectionPolygon();
        }

        SelectionAnchorCaret UpdateCaret(SelectionAnchor anchor, SelectionAnchorCaret caret, ref bool updated)
        {
            if (anchor != null && caret == null)
            {
                updated = true;
                caret = new SelectionAnchorCaret() { Anchor = anchor };

                var content = Owner.Content;
                if (anchor.Chunk.SequenceNumber < content.TextLayer.Items.FirstOrDefault()?.Source.BaseChunk.SequenceNumber)
                {
                    caret.Bounds = new Rect(0, -content.LineHeight, 0, 0);
                }
                else if (anchor.Chunk.SequenceNumber > content.TextLayer.Items.LastOrDefault()?.Source.BaseChunk.SequenceNumber)
                {
                    caret.Bounds = new Rect(0, Math.Max(content.Metrics.Viewport.Height, content.TextLayer.Height) + content.LineHeight, 0, 0);
                }
                else
                {
                    var chunkView = content.TryGetChunkView(anchor.Chunk.SequenceNumber);
                    if (chunkView != null)
                    {
                        if (chunkView.FirstFragmentIndex != -1 && chunkView.LastFragmentIndex != -1)
                            chunkView.PrepareSelectionAnchor(caret);
                        else
                            Debug.Assert(false);
                    }
                }
            }

            return caret;
        }
    }

}
