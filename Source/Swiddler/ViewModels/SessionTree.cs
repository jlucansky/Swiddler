using Swiddler.Common;
using Swiddler.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Swiddler.ViewModels
{
    public class SessionTree
    {
        public event EventHandler<Session> ChildSessionAdded;
        public event EventHandler<int> ItemAdded;

        public IReadOnlyList<Session> RootSessions => _RootSessions;
        private readonly List<Session> _RootSessions;

        public IReadOnlyList<SessionListItem> FlattenItems => _FlattenItems; // bind to ListView
        private readonly ObservableCollection<SessionListItem> _FlattenItems;

        private class SessionEntry
        {
            public SessionListItem SessionListItem;
            public List<SessionListItem> ChildItems;
        }

        readonly Dictionary<Session, SessionEntry> sessionMap;

        public SessionTree()
        {
            _RootSessions = new List<Session>();
            _FlattenItems = new ObservableCollection<SessionListItem>();
            sessionMap = new Dictionary<Session, SessionEntry>();
        }

        public void Add(Session session)
        {
            var entry = new SessionEntry()
            {
                SessionListItem = new SessionListItem(session), // root
                ChildItems = session.Children.Select(x => new SessionListItem(x)).ToList(),
            };

            _RootSessions.Add(session);
            _FlattenItems.Add(entry.SessionListItem);
            sessionMap.Add(session, entry);

            if (entry.SessionListItem.HasToggleButton)
                Expand(_FlattenItems.Count - 1);

            session.ChildSessionAdded += HandleNewChildSession;

            RaiseItemAdded(_FlattenItems.Count - 1);
        }

        public void Remove(int itemIndex)
        {
            var item = _FlattenItems[itemIndex];
            var session = item.Session;

            if (sessionMap.TryGetValue(session, out var entry))
            {
                session.ChildSessionAdded -= HandleNewChildSession;
                sessionMap.Remove(session);
            }

            _FlattenItems.RemoveRange(itemIndex, (item.IsExpanded ? session.Children.Count : 0) + 1);
        }

        private void HandleNewChildSession(object sender, Session child)
        {
            var parentSession = (Session)sender;
            var entry = sessionMap[parentSession];
            var rootItem = entry.SessionListItem; // owner
            var childItem = new SessionListItem(child);

            entry.ChildItems.Add(childItem);


            if (rootItem.HasToggleButton == false)
            {
                rootItem.HasToggleButton = true;
                rootItem.IsExpanded = true;
            }

            if (rootItem.IsExpanded == true)
            {
                var index = _FlattenItems.IndexOf(rootItem);

                if (index < 0)
                    throw new Exception("Session owner not found");

                _FlattenItems.Insert(index + entry.ChildItems.Count, childItem);

                RaiseItemAdded(index + entry.ChildItems.Count);
            }
            else
            {
                rootItem.IsFlagged = true;
            }

            ChildSessionAdded?.Invoke(sender, child);
        }

        public void Expand(int itemIndex)
        {
            var item = _FlattenItems[itemIndex];
            var session = item.Session;

            if (item.IsExpanded == false && sessionMap.TryGetValue(session, out var entry))
            {
                _FlattenItems.InsertRange(itemIndex + 1, entry.ChildItems);
                item.IsExpanded = true;
            }
        }

        public void Collapse(int itemIndex)
        {
            var item = _FlattenItems[itemIndex];
            var session = item.Session;

            if (item.IsExpanded == true && sessionMap.TryGetValue(session, out var entry))
            {
                _FlattenItems.RemoveRange(itemIndex + 1, entry.ChildItems.Count);
                item.IsExpanded = false;
            }
        }

        public int IndexOf(SessionListItem item) => _FlattenItems.IndexOf(item);

        public SessionListItem GetItem(Session session)
        {
            if (sessionMap.TryGetValue(session, out var entry))
                return entry.SessionListItem;
            return null;
        }

        private void RaiseItemAdded(int index)
        {
            ItemAdded?.Invoke(this, index);
        }
    }
}
