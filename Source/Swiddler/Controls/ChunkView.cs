using Swiddler.Common;
using Swiddler.Rendering;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Swiddler.Controls
{
    /// <summary>
    /// Add this XmlNamespace attribute to the root element of the markup file where it is to be used:
    ///     xmlns:MyNamespace="clr-namespace:Swiddler.Controls"
    /// </summary>
    public class ChunkView : Control
    {
        #region ScrollBarVisibility
        /// <summary>
        /// Dependency property for <see cref="HorizontalScrollBarVisibility"/>
        /// </summary>
        public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty = ScrollViewer.HorizontalScrollBarVisibilityProperty.AddOwner(typeof(ChunkView), new FrameworkPropertyMetadata(ScrollBarVisibility.Hidden));

        /// <summary>
        /// Gets/Sets the horizontal scroll bar visibility.
        /// </summary>
        public ScrollBarVisibility HorizontalScrollBarVisibility
        {
            get { return (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty); }
            set { SetValue(HorizontalScrollBarVisibilityProperty, value); }
        }

        /// <summary>
        /// Dependency property for <see cref="VerticalScrollBarVisibility"/>
        /// </summary>
        public static readonly DependencyProperty VerticalScrollBarVisibilityProperty = ScrollViewer.VerticalScrollBarVisibilityProperty.AddOwner(typeof(ChunkView), new FrameworkPropertyMetadata(ScrollBarVisibility.Visible));

        /// <summary>
        /// Gets/Sets the vertical scroll bar visibility.
        /// </summary>
        public ScrollBarVisibility VerticalScrollBarVisibility
        {
            get { return (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty); }
            set { SetValue(VerticalScrollBarVisibilityProperty, value); }
        }
        #endregion

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, FragmentView.CopySelection, FragmentView.CanCopySelection));
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
        }

        public FragmentView FragmentView { get; } = new FragmentView();

        public Session CurrentSession => FragmentView?.CurrentSession;

        public void SetSession(Session session)
        {
            FragmentView.SetSession(session);
        }

        static ChunkView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ChunkView), new FrameworkPropertyMetadata(typeof(ChunkView)));
        }

    }
}
