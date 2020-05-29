using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Windows.Media;

namespace Swiddler.Rendering
{
    public class VisualPool : IDisposable
    {
        readonly Func<Visual> CreateVisual;
        readonly Stack<Visual> items = new Stack<Visual>();

        static readonly Type[] emptyTypes = new Type[0];

        public VisualPool(Type visualType)
        {
            // faster instantiation
            CreateVisual = Expression.Lambda<Func<Visual>>(Expression.New(visualType.GetConstructor(emptyTypes))).Compile();
        }

        public Visual GetFresh()
        {
            if (items.Count == 0)
                return CreateVisual();

            return items.Pop();
        }

        public void Recycle(Visual visual)
        {
            items.Push(visual);
        }

        public void Dispose()
        {
            foreach (var item in items)
                (item as IDisposable)?.Dispose();
            items.Clear();
        }
    }
}
