using System;
using System.Windows;
using System.Windows.Media;

namespace Swiddler.Rendering
{
    public abstract class DrawingFragment : Fragment
    {
        public override Type VisualType => typeof(DrawingVisual);

        protected override void Refresh()
        {
            var drawingVisual = (DrawingVisual)Visual;

            using (var context = drawingVisual.RenderOpen())
            {
                OnRender(context, Bounds);
            }
        }

        public abstract void OnRender(DrawingContext drawingContext, Rect bounds);
    }
}
