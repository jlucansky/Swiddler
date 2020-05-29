using Swiddler.Common;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Swiddler.ViewModels
{
    public class SessionListItem : BindableBase
    {
        public Session Session { get; }


        bool _IsExpanded;
        public bool IsExpanded { get => _IsExpanded; set => SetProperty(ref _IsExpanded, value); }

        bool _HasToggleButton; // has expandable toggle button
        public bool HasToggleButton { get => _HasToggleButton; set => SetProperty(ref _HasToggleButton, value); }


        int _IndentLevel;
        public int IndentLevel { get => _IndentLevel; set => SetProperty(ref _IndentLevel, value); }


        string _Title;
        public string Title { get => _Title; set => SetProperty(ref _Title, value); }

        SolidColorBrush _StateBrush;
        public SolidColorBrush StateBrush { get => _StateBrush; set => SetProperty(ref _StateBrush, value); }


        string _Counters; // in / out
        public string Counters { get => _Counters; set { if (SetProperty(ref _Counters, value) && !_IsSelected) IsFlagged = true; } }


        bool _IsFlagged; // indicate unread item when counter changed
        public bool IsFlagged { get => _IsFlagged; set => SetProperty(ref _IsFlagged, value); }

        ImageSource _Icon;
        public ImageSource Icon { get => _Icon; set => SetProperty(ref _Icon, value); }


        ProcessInfo _Process;
        public ProcessInfo Process { get => _Process; set => SetProperty(ref _Process, value); }


        int? _PID;
        public int? PID { get => _PID; set { if (SetProperty(ref _PID, value)) UpdateProcessInfoAsync(); } }


        bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set => SetProperty(ref _IsSelected, value); }


        public override string ToString()
        {
            return Title;
        }

        public SessionListItem() { }

        public SessionListItem(Session session)
        {
            Session = session;

            session.PropertyChanged += Session_PropertyChanged;

            _Title = session.Name;
            _HasToggleButton = session.Children.Any();
            _IndentLevel = session.Parent == null ? 0 : 1;
            _PID = session.PID == 0 ? null : (int?)session.PID;
            _Counters = Counters;

            UpdateStateBrush(Session.State);
            UpdateProcessInfoAsync();
        }

        static readonly SolidColorBrush StartingBrush = FindSessionStateBrush(SessionState.Starting);
        static readonly SolidColorBrush StartedBrush = FindSessionStateBrush(SessionState.Started);
        static readonly SolidColorBrush StoppedBrush = FindSessionStateBrush(SessionState.Stopped);

        static SolidColorBrush FindSessionStateBrush(SessionState state) => (SolidColorBrush)Application.Current.FindResource("Swiddler.Session." + state);

        void UpdateStateBrush(SessionState state)
        {
            switch (state)
            {
                case SessionState.Starting: StateBrush = StartingBrush; break;
                case SessionState.Started: StateBrush = StartedBrush; break;
                case SessionState.Stopped:
                case SessionState.Error:
                    StateBrush = StoppedBrush;
                    break;
            }
        }

        private Task UpdateProcessInfoAsync()
        {
            return Task.Run(() => Process = ProcessInfo.Get(_PID));
        }

        private void Session_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Session.Name): Title = Session.Name; break;
                case nameof(Session.State): UpdateStateBrush(Session.State); break;
                case nameof(Session.PID): PID = Session.PID == 0 ? null : (int?)Session.PID; break;
                case nameof(Session.CountersFormatted): Counters = Session.CountersFormatted; break;
            }
        }
    }
}
