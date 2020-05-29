using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Swiddler.Common
{
    public interface IHasReload
    {
        void Reload();
    }

    public class ReloadableCollection<T> : ObservableCollection<T>, IHasReload
    {
        readonly Func<IEnumerable<T>> itemsFactory;

        public ReloadableCollection(Func<IEnumerable<T>> itemsFactory) : base(itemsFactory())
        {
            this.itemsFactory = itemsFactory;
        }

        public void Reload()
        {
            var list = (List<T>)Items;
            var newItems = itemsFactory();
            
            list.Clear();
            list.AddRange(newItems);
            
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
