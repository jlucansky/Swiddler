using Swiddler.Common;
using Swiddler.IO;
using Swiddler.Serialization.Rtf;
using Swiddler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Swiddler.Rendering
{
    public partial class FragmentView : FrameworkElement, IScrollInfo
    {
        public TranslateTransform ScrollTransform { get; } = new TranslateTransform();

        public List<FragmentLayer> Layers { get; } = new List<FragmentLayer>();

        public FragmentLayer BackgroundLayer { get; }
        public FragmentLayer TextLayer { get; }
        public SelectionLayer SelectionLayer { get; }

        public FragmentViewContent Content { get; private set; }
        public Session CurrentSession { get; private set; }

        public MouseState Mouse { get; } = new MouseState();


        public event EventHandler<double> OleProgressChanged; // reporting progress for clipboard / drag & drop
        public event EventHandler<Session> SessionChanged;

        private FragmentViewMetrics Metrics => Content.Metrics;

        private readonly Dictionary<Type, VisualPool> _VisualPools = new Dictionary<Type, VisualPool>();

        internal bool UpdateScrollInfo { get; set; }
        private bool ForceGarbageCollection { get; set; }

        public FragmentView()
        {
            WheelScrollLines = SystemParameters.WheelScrollLines;

            UpdateCursor();

            BackgroundLayer = new FragmentLayer(this);
            SelectionLayer = new SelectionLayer(this);
            TextLayer = new FragmentLayer(this);

            Layers.Add(BackgroundLayer);
            Layers.Add(SelectionLayer);
            Layers.Add(TextLayer);

            ScrollTransform.Changed += (s, e) => UpdateMousePosition(null); ;

            foreach (var layer in Layers)
            {
                AddVisualChild(layer.Container);
                AddLogicalChild(layer.Container);
            }

            CreateContent();
        }

        public void CreateContent()
        {
            Content = new FragmentViewContent()
            {
                View = this,
                TextLayer = TextLayer,
                BackgroundLayer = BackgroundLayer,
                SelectionLayer = SelectionLayer,
                Session = CurrentSession,
                Metrics = new FragmentViewMetrics()
                {
                    Encoding = Encoding.GetEncoding(437), // IBM 437 (OEM-US)
                    Typeface = new Typeface("Lucida Console"),
                    FontSize = 14,
                }
            };

            Content.ComputeDpi();
        }

        public ScrollViewer ScrollOwner { get; set; }

        public void LineDown() { SetVerticalOffset(Metrics.CurrentLineIndex + 1); }

        public void LineUp() { SetVerticalOffset(Metrics.CurrentLineIndex - 1); }

        public void LineLeft() { SetHorizontalOffset(HorizontalOffset - Content.CharWidth); }

        public void LineRight() { SetHorizontalOffset(HorizontalOffset + Content.CharWidth); }

        public double WheelScrollLines { get; set; }

        public void MouseWheelDown() { SetVerticalOffset(Metrics.CurrentLineIndex + WheelScrollLines); }

        public void MouseWheelUp() { SetVerticalOffset(Metrics.CurrentLineIndex - WheelScrollLines); }

        public void MouseWheelLeft() { SetHorizontalOffset(HorizontalOffset - WheelScrollLines * Content.CharWidth); }

        public void MouseWheelRight() { SetHorizontalOffset(HorizontalOffset + WheelScrollLines * Content.CharWidth); }

        public void PageDown() { SetVerticalOffset(VerticalOffset + ViewportHeight); }

        public void PageUp() { SetVerticalOffset(VerticalOffset - ViewportHeight); }

        public void PageLeft() { SetHorizontalOffset(HorizontalOffset - ViewportWidth); }

        public void PageRight() { SetHorizontalOffset(HorizontalOffset + ViewportWidth); }

        public bool CanHorizontallyScroll { get; set; }

        public bool CanVerticallyScroll { get; set; }

        public double ExtentHeight => Math.Max(0, Metrics.LinesCountApprox + Metrics.OverExtentLines); // height is in "lines", not pixels

        public double ExtentWidth { get; set; }

        public double HorizontalOffset { get; set; }

        public double VerticalOffset => Metrics.CurrentLineIndex;

        public double ViewportHeight => Metrics.LinesPerPage;

        public double ViewportWidth => Metrics.Viewport.Width;

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            // TODO
            return Rect.Empty;
        }

        public void SetHorizontalOffset(double offset)
        {
            offset = Content.SnapToPixelsX(Math.Max(0, Math.Min(offset, ExtentWidth - ViewportWidth)), ceiling: true);
            if (offset != HorizontalOffset)
            {
                HorizontalOffset = offset;
                InvalidateArrange();
            }
        }

        public void SetVerticalOffset(double startLine)
        {
            startLine = Math.Max(0, Math.Min(ExtentHeight - ViewportHeight, startLine));
            if (Metrics.CurrentLineIndex != startLine)
            {
                Metrics.CurrentLineIndex = startLine;
                Metrics.CurrentFileOffset = Metrics.GetOffsetFromLine(startLine);
                InvalidateOffset();
            }
        }

        public void SetFileOffset(long offset)
        {
            offset = Math.Max(Metrics.StartFileOffset, Math.Min(Metrics.MaxFileLength, offset));
            if (Metrics.CurrentFileOffset != offset)
            {
                Metrics.CurrentFileOffset = offset;
                Metrics.CurrentLineIndex = Metrics.GetLineFromOffset(offset);
                InvalidateOffset();
            }
        }

        void InvalidateOffset() // call after changing VerticalOffset or FileOffset
        {
            UpdateScrollInfo = true;
            InvalidateArrange();
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);
            Content.ComputeDpi(); // DPI factor should be acessible for this view
        }

        protected override int VisualChildrenCount => Layers.Count;

        protected override Visual GetVisualChild(int index) => Layers[index].Container;

        protected override Size MeasureOverride(Size constraint)
        {
            if (CurrentSession != null)
            {
                bool widthChanged = Metrics.Viewport.Width != constraint.Width;
                //int oldLineLength = Content.LineLength;

                if (constraint.Width < 50)
                    constraint.Width = 50;

                Metrics.Viewport = constraint;
                Metrics.LinesPerPage = constraint.Height / Content.LineHeight;
                Metrics.OverExtentLines = Metrics.LinesPerPage;

                UpdateStorageMetrics();

                //if (Content.LineLength != oldLineLength)
                if (widthChanged)
                {
                    Content.Reset(); // rebuild all when LineLength changes
                    ForceGarbageCollection = true;
                }
            }

            return constraint;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (CurrentSession != null)
            {
                Content.Invalidate();

                foreach (var layer in Layers)
                {
                    layer.ArrangeViewport();
                }

                if (UpdateScrollInfo)
                {
                    UpdateScrollInfo = false;
                    ScrollOwner?.InvalidateScrollInfo();
                }
            }

            if (ForceGarbageCollection)
            {
                ForceGarbageCollection = false;
                GC.Collect(); // continuous gc collect ensures smooth window resizing
            }

            return finalSize;
        }

        public void Clear()
        {
            foreach (var layer in Layers)
            {
                layer.Clear();
            }

            Content.InsertionPoint = new Point();
        }

        public VisualPool GetVisualPool(Type visualType)
        {
            if (_VisualPools.TryGetValue(visualType, out var pool) == false)
            {
                pool = new VisualPool(visualType);
                _VisualPools[visualType] = pool;
            }
            return pool;
        }

        public void SetSession(Session session)
        {
            if (session == CurrentSession)
                return;

            SessionChanged?.Invoke(this, session);

            Content.Reset(); // clear all fragments & chunks cache

            if (CurrentSession != null)
            {
                CurrentSession.Storage.BlockIterator.ClearCache();
                CurrentSession.StorageDataChanged -= StorageDataChanged; ;
                CurrentSession.ViewContent = Content; // backup state
            }

            CurrentSession = session;
            CurrentSession.StorageDataChanged += StorageDataChanged;

            if (CurrentSession.ViewContent is FragmentViewContent viewContent)
            {
                Content = viewContent; // restore state
            }
            else
            {
                CreateContent(); // new session
            }

            Mouse.IsSelecting = false; // reset ongoing selection

            InvalidateMeasure(); // rebuild all fragments
        }

        private void UpdateStorageMetrics()
        {
            Metrics.MaxFileLength = CurrentSession.Storage.FileLength;
            Metrics.LinesCountApprox = Math.Max(CurrentSession.Storage.LinesCountApprox, Metrics.UsableFileLength / (double)Content.LineLength);
            UpdateScrollInfo = true;
        }

        private void StorageDataChanged(long atOffset)
        {
            UpdateStorageMetrics();

            if (atOffset == -1)
            {
                Content.Reset(); // force reload
                InvalidateArrange();
            }
            else if (Content.ShouldArrange(atOffset))
                InvalidateArrange();
            else
                ScrollOwner?.InvalidateScrollInfo();
        }

        public void CopySelection(object sender, ExecutedRoutedEventArgs e)
        {
            if (Content.HasSelection)
            {
                if (Math.Abs(Content.SelectionEnd.Chunk.ActualOffset- Content.SelectionStart.Chunk.ActualOffset) > 1024 * 1024)
                {
                    MessageBox.Show("Selection is too large. Use Drag & Drop instead.", "Clipboard", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                try
                {
                    using (var stream = new MemoryStream())
                    {
                        var rtf = new RtfDocument();
                        var target = new CompositeChunkWriter(new StreamChunkWriter(stream), new RtfChunkWriter(rtf, Metrics.Encoding));
                        
                        var transfer = new DataTransfer(CurrentSession.Storage, target);
                        transfer.CopySelection(Content.SelectionStart, Content.SelectionEnd);

                        var bytes = stream.ToArray();

                        DataObject data = new DataObject();
                        data.SetData(DataFormats.Text, Metrics.Encoding.GetString(bytes));
                        data.SetData(DataFormats.Rtf, rtf.render());

                        if (bytes.IsBinary())
                            data.SetData(bytes.GetType().ToString(), bytes);

                        Clipboard.SetDataObject(data);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void CanCopySelection(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Content.HasSelection;
        }

        void DragSelection()
        {
            try
            {
                bool savePcap = App.Current.PcapSelectionExport;

                string name = $"cap-{DateTime.Now:yyyyMMdd-HHmmss}";

                if (savePcap) name += ".pcap";

                using (var cancellation = new CancellationTokenSource())
                using (var stream = CurrentSession.Storage.CreateTempFile(name))
                {
                    IChunkWriter target;

                    if (savePcap)
                        target = new PcapChunkWriter(stream, CurrentSession.ProtocolType);
                    else
                        target = new StreamChunkWriter(stream);

                    using (target as IDisposable)
                    {
                        var transfer = new DataTransfer(CurrentSession.Storage, target) { CancellationToken = cancellation.Token };
                        transfer.ProgressChanged += (_, val) => ReportOleProgress(val);
                        var copyTask = transfer.CopySelectionAsync(Content.SelectionStart, Content.SelectionEnd);
                        var dataObject = new DeferredDataObject();
                        dataObject.SetSwiddlerSelection();
                        dataObject.SetData(DataFormats.FileDrop, () => { copyTask.Wait(); stream.Flush(); return new[] { stream.Name }; });
                        if (DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy) == DragDropEffects.None) cancellation.Cancel();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            ReportOleProgress(0);
        }

        void ReportOleProgress(double value)
        {
            OleProgressChanged?.Invoke(this, value);
        }

        private Point _lastMousePosition;
        void UpdateMousePosition(MouseEventArgs e)
        {
            if (e != null) _lastMousePosition = e.GetPosition(this);
            Mouse.Position = _lastMousePosition - new Vector(ScrollTransform.X, ScrollTransform.Y);
        }

        public void UpdateCursor()
        {
            Cursor = Mouse.IsOverSelection ? Cursors.Arrow : Cursors.IBeam;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (Mouse.LeftButtonDown && !Mouse.ShiftDown && Mouse.IsOverSelection)
            {
                DragSelection();
            }

            UpdateMousePosition(e);
            MouseChanged();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Mouse.LeftButtonDown = true;

            if (!Mouse.ShiftDown)
            {
                if (Mouse.IsOverSelection) return; // drag on MouseMove

                ResetSelection();
            }

            if (e.ClickCount == 1)
            {
                Mouse.SelectWholeChunks = false;
            }
            else if (e.ClickCount == 2)
            {
                Mouse.SelectWholeChunks = true;
            }

            System.Windows.Input.Mouse.Capture(this);
            Mouse.IsSelecting = true;
            UpdateMousePosition(e);
            MouseChanged();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (Mouse.LeftButtonDown == false) return; // not initiated from this control

            if (Mouse.IsSelecting == false || Content.SelectionStart?.Equals(Content.SelectionEnd) == true)
                ResetSelection();

            Mouse.LeftButtonDown = false;
            Mouse.IsSelecting = false;
            System.Windows.Input.Mouse.Capture(null);

            UpdateMousePosition(e);
            MouseChanged();
            InvalidateArrange();
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            Mouse.IsOverView = true;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            Mouse.IsSelecting = false;
            Mouse.IsOverView = false;
            Mouse.LeftButtonDown = false;
            Mouse.SelectWholeChunks = false;
            MouseChanged();
        }

        void MouseChanged()
        {
            foreach (var layer in Layers)
            {
                layer.MouseChanged(Mouse);
            }
        }

        public void ResetSelection()
        {
            Content.SelectionStart = null;
            Content.SelectionEnd = null;
            SelectionLayer.ResetSelection();
        }

        public void SelectAll()
        {
            using (var reader = CurrentSession.Storage.CreateReader())
            {
                var first = reader.GetFirstPacket(p => p.Payload.Length > 0);
                var last = reader.GetLastPacket(p => p.Payload.Length > 0);

                if (first != null && last != null)
                {
                    Content.SetSelection(first, last);
                    InvalidateArrange();
                }
            }
        }

    }

    public class MouseState
    {
        public Point Position = new Point(-1, -1);

        public bool IsSelecting;
        public bool IsOverView;
        public bool IsOverSelection;
        public bool LeftButtonDown;
        public bool SelectWholeChunks;

        public bool IsCaptured => Mouse.Captured != null;
        public bool ShiftDown => Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
    }

}
