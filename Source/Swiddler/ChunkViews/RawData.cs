using Swiddler.Common;
using Swiddler.DataChunks;
using Swiddler.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace Swiddler.ChunkViews
{
    public class RawData : ChunkViewItem<Packet>
    {
        public override void Build()
        {
            byte[] data = Chunk.Payload;

            double lenScale = 1;
            double approxFileOffset = Chunk.ActualOffset;
            bool hasFragment = false;

            Point start = new Point(), end = new Point();
            Brush textBrush = GetTextBrush();

            if (Chunk.ActualLength > 0 && data.Length > 0)
                lenScale = Chunk.ActualLength / (double)data.Length;

            var snappedWidth = ViewContent.SnapToPixelsX(ViewContent.Metrics.Viewport.Width - ViewContent.DpiScale.X);

            for (int i = 0; i < data.Length;)
            {
                int remainingLineLen = (int)((ViewContent.Metrics.Viewport.Width - ViewContent.InsertionPoint.X + 0.0000001) / ViewContent.CharWidth);

                Debug.Assert(remainingLineLen > 0);

                if (i >= data.Length)
                    break;
                
                int len = GetLineLength(i, Math.Min(remainingLineLen, data.Length - i), out var reachedNewLine);

                Rect bounds;
                int endLineOffset = i + len;
                if (reachedNewLine)
                {
                    byte cr = data[endLineOffset];
                    endLineOffset++;
                    if (endLineOffset < data.Length)
                    {
                        byte lf = data[endLineOffset];
                        if (cr == 13 && lf == 10)
                            endLineOffset++;
                    }

                    bounds = new Rect(ViewContent.InsertionPoint, new Point(snappedWidth, ViewContent.InsertionPoint.Y + ViewContent.LineHeight));
                }
                else
                {
                    bounds = new Rect(ViewContent.InsertionPoint, new Size(ViewContent.SnapToPixelsX(len * ViewContent.CharWidth, ceiling: true), ViewContent.LineHeight));
                }

                double approxFileLen;
                if (endLineOffset == data.Length) // set approx length to exactly remaining bytes
                    approxFileLen = Chunk.ActualOffset + Chunk.ActualLength - (long)approxFileOffset;
                else
                    approxFileLen = (endLineOffset - i) * lenScale;

                var fragment = new TextFragment()
                {
                    Source = this,
                    Data = data,
                    Offset = i,
                    Length = len,
                    Encoding = ViewContent.Metrics.Encoding,
                    Brush = textBrush,
                    Bounds = bounds,
                    ApproxFileOffset = (long)approxFileOffset,
                    ApproxFileLength = (int)approxFileLen,
                    IsSelectable = true,
                };

                ViewContent.TextLayer.Add(fragment);
                ViewContent.MoveInsertionPointAfter(fragment);

                if (!hasFragment)
                {
                    start = fragment.Bounds.TopLeft;
                    hasFragment = true;
                }
                end = fragment.Bounds.TopRight;

                approxFileOffset += approxFileLen;
                i = endLineOffset;
            }

            if (hasFragment)
                CreateBackgroundFragment(start, end, snappedWidth);
        }

        private void CreateBackgroundFragment(Point start, Point end, double snappedWidth)
        {
            if (Chunk.Flow != TrafficFlow.Undefined)
            {
                var fragment = new FilledFragment(start, end, ViewContent.LineHeight, snappedWidth) { Source = this };

                fragment.MouseEnter = () => UpdateHighlightState(fragment, true);
                fragment.MouseLeave = () => UpdateHighlightState(fragment, false);

                if (Chunk.Flow == TrafficFlow.Inbound)
                    fragment.Brush = App.Current.Res.InboundFlowBrush;
                if (Chunk.Flow == TrafficFlow.Outbound)
                    fragment.Brush = App.Current.Res.OutboundFlowBrush;

                ViewContent.BackgroundLayer.Add(fragment);
            }
        }

        private Brush GetTextBrush()
        {
            if (Chunk.Flow == TrafficFlow.Inbound)
                return App.Current.Res.InboundFlowTextBrush;
            if (Chunk.Flow == TrafficFlow.Outbound)
                return App.Current.Res.OutboundFlowTextBrush;

            return Brushes.Black;
        }

        private void UpdateHighlightState(FilledFragment fragment, bool highlighted)
        {
            var res = App.Current.Res;
            var flow = Chunk.Flow;

            var selLayer = fragment.View.SelectionLayer;
            var highlight = selLayer.HighlightFragment;

            if (highlighted)
            {
                selLayer.CurrentlyHighlightedFragment = fragment;
                highlight.Polgyons = fragment.Polgyons;

                if (flow == TrafficFlow.Inbound)
                {
                    highlight.Brush = res.InboundSelectedFlowBrush;
                    highlight.Pen = res.InboundFlowPen;
                }
                if (flow == TrafficFlow.Outbound)
                {
                    highlight.Brush = res.OutboundSelectedFlowBrush;
                    highlight.Pen = res.OutboundFlowPen;
                }
            }
            else
            {
                if (selLayer.CurrentlyHighlightedFragment == fragment)
                {
                    selLayer.CurrentlyHighlightedFragment = null;
                    highlight.Polgyons = null;
                }
            }

            selLayer.ArrangeViewport();
        }

        int GetLineLength(int offset, int count, out bool reachedNewLine)
        {
            reachedNewLine = false;

            var data = Chunk.Payload;
            var end = offset + count;

            for (int i = offset; i < end; i++)
            {
                if (data[i] == 13 || data[i] == 10 || data[i] == 12)
                {
                    reachedNewLine = true;
                    return i - offset;
                }
            }

            return count;
        }

        public override void PrepareSelectionAnchor(SelectionAnchorCaret caret)
        {
            if (caret.Fragment == null)
            {
                Debug.Assert(Chunk.SequenceNumber == caret.Anchor?.Chunk.SequenceNumber);

                // find fragment at anchor offset
                var offset = caret.Anchor.Offset;
                int index = ViewContent.TextLayer.FindInRange(FirstFragmentIndex, LastFragmentIndex, f => ((TextFragment)f).Offset <= offset ? -1 : 1) - 1;
                caret.Fragment = ViewContent.TextLayer.Items[index];
            }

            var fragment = caret.Fragment;
            var txtFrag = (TextFragment)fragment;
            var txtFragOffset = txtFrag.Offset;
            var txtFragLen = txtFrag.Length;
            var charWidth = fragment.View.Content.CharWidth;
            var fragLoc = fragment.Bounds.Location;
            var toTheEnd = false;

            if (caret.Anchor == null)
            {
                Debug.Assert(caret.Fragment?.IsSelectable == true);
                Debug.Assert(Chunk == caret.Fragment.Source.BaseChunk);

                caret.Anchor = new SelectionAnchor() { Chunk = Chunk };

                if (fragment.View.Mouse.Position.Y > fragment.Bounds.Bottom)
                {
                    caret.Anchor.Offset = fragment.IsTrailing ? txtFrag.Data.Length : txtFragLen + txtFragOffset;
                    toTheEnd = true;
                }
                else
                {
                    double pos = fragment.View.Mouse.Position.X + charWidth / 2.0;
                    caret.Anchor.Offset = Math.Min(txtFragLen, Math.Max(0, (int)((pos - fragLoc.X) / charWidth))) + txtFragOffset;
                }
            }
            else
            {
                if (caret.Anchor.Offset >= Chunk.Payload.Length)
                    toTheEnd = true;
            }

            caret.Bounds = new Rect(
                toTheEnd ? fragment.Bounds.Right : fragLoc.X + (caret.Anchor.Offset - txtFragOffset) * charWidth,
                fragLoc.Y,
                charWidth,
                fragment.Bounds.Height);
        }

    }

}
