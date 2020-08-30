using System.Windows.Input;

namespace Swiddler.Commands
{
    public static class MiscCommands
    {
        public static RoutedCommand QuickConnect { get; } = new RoutedCommand(nameof(QuickConnect), typeof(MiscCommands));
        public static RoutedCommand Send { get; } = new RoutedCommand(nameof(Send), typeof(MiscCommands));
        public static RoutedCommand Disconnect { get; } = new RoutedCommand(nameof(Disconnect), typeof(MiscCommands));
        public static RoutedCommand Search { get; } = new RoutedCommand(nameof(Search), typeof(MiscCommands));
        public static RoutedCommand SelectAll { get; } = new RoutedCommand(nameof(SelectAll), typeof(MiscCommands));
        public static RoutedCommand GoToStart { get; } = new RoutedCommand(nameof(GoToStart), typeof(MiscCommands));
        public static RoutedCommand GoToEnd { get; } = new RoutedCommand(nameof(GoToEnd), typeof(MiscCommands));
        public static RoutedCommand ToggleHex { get; } = new RoutedCommand(nameof(ToggleHex), typeof(MiscCommands));
    }
}
