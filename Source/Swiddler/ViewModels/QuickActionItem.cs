using Swiddler.Utils;
using System;
using System.ComponentModel;
using System.Globalization;

namespace Swiddler.ViewModels
{
    public class QuickActionItem
    {
        public string Icon { get; set; }
        public string Caption { get; set; }
        public string Description { get; set; }

        public QuickActionTemplate Template { get; set; }

        protected Action<ConnectionSettings> Builder { get; set; }

        public static QuickActionItem[] DefaultTemplates { get; } = new[]
        {
            new QuickActionItem(QuickActionTemplate.ClientTCPv4),
            new QuickActionItem(QuickActionTemplate.ServerTCPv4),
            new QuickActionItem(QuickActionTemplate.TunnelTCPv4),
            //new QuickActionItem(QuickActionTemplate.Monitor),
        };

        public QuickActionItem(QuickActionTemplate template)
        {
            Template = template;

            switch (template)
            {
                case QuickActionTemplate.ClientTCPv4:
                    Icon = "Connect";
                    Description = "Client (connect to host)";
                    Builder = cs => { cs.TCPChecked = true; cs.ClientChecked = true; };
                    break;
                case QuickActionTemplate.ServerTCPv4:
                    Icon = "Port";
                    Description = "Server (open local port)";
                    Builder = cs => { cs.TCPChecked = true; cs.ServerChecked = true; };
                    break;
                case QuickActionTemplate.TunnelTCPv4:
                    Icon = "Tunnel";
                    Description = "Tunnel (client & server)";
                    Builder = cs => { cs.TCPChecked = true; cs.ClientChecked = true; cs.ServerChecked = true; };
                    break;
                case QuickActionTemplate.Monitor:
                    Icon = "Process";
                    Description = "Process traffic monitor";
                    Builder = cs => {}; // TODO
                    break;
            }
        }

        public virtual bool MatchSearch(string textToFind)
        {
            if (string.IsNullOrEmpty(textToFind) || string.IsNullOrEmpty(Description)) return true;
            return Description.IndexOf(textToFind, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public virtual ConnectionSettings GetConnectionSettings(ConnectionSettings recent)
        {
            if (Builder != null)
            {
                var cs = ConnectionSettings.New();
                Builder(cs);
                return cs;
            }

            throw new NotImplementedException($"Template for '{Template}' is not implemented.");
        }
    }

    public enum QuickActionTemplate
    {
        Undefined,
        ClientTCPv4,
        ServerTCPv4,
        TunnelTCPv4,
        Monitor
    }

    public class QuickActionGroupDescription : GroupDescription
    {
        public static QuickActionGroupDescription Default { get; } = new QuickActionGroupDescription();

        public override object GroupNameFromItem(object item, int level, CultureInfo culture)
        {
            if (item is RecentlyUsedItem recent)
            {
                var cs = recent.ConnectionSettings;

                var dt = cs.CreatedAt.Date;
                var now = DateTime.Now.Date;

                var yesterday = now.AddDays(-1);
                var thisWeekStart = now.LastDayOfWeek(DayOfWeek.Monday);
                var lastWeekStart = thisWeekStart.AddDays(-7);
                var thisMonthStart = new DateTime(now.Year, now.Month, 1);
                var lastMonthStart = thisMonthStart.AddMonths(-1);

                if (dt > now)
                    return "Future";
                if (dt == now)
                    return "Today";
                if (dt >= yesterday)
                    return "Yesterday";
                if (dt >= thisWeekStart)
                    return "This week";
                if (dt >= lastWeekStart)
                    return "Last week";
                if (dt >= thisMonthStart)
                    return "This month";
                if (dt >= lastMonthStart)
                    return "Last month";

                return "Older";
            }
            else if (item is QuickActionItem)
            {
                return "Create new";
            }

            return null;
        }
    }
}
