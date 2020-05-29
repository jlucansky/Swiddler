using System.Windows.Input;

namespace Swiddler.Commands
{
    public static class MiscCommands
    {
        public static RoutedCommand QuickConnect { get; } = new RoutedCommand(nameof(QuickConnect), typeof(MiscCommands));
        public static RoutedCommand Send { get; } = new RoutedCommand(nameof(Send), typeof(MiscCommands));
        public static RoutedCommand Disconnect { get; } = new RoutedCommand(nameof(Disconnect), typeof(MiscCommands));
        public static RoutedCommand Search { get; } = new RoutedCommand(nameof(Search), typeof(MiscCommands));

    }
}
