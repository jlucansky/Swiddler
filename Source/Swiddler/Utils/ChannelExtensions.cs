using Swiddler.Channels;
using System.Collections.Generic;

namespace Swiddler.Utils
{
    public static class ChannelExtensions
    {
        public static ICollection<Channel> FindAllChannels(this IEnumerable<Channel> channels)
        {
            var knownChannels = new HashSet<Channel>();
            foreach (var channel in channels)
                VisitRecipients(channel, knownChannels);
            return knownChannels;
        }

        public static ICollection<Channel> FindAllChannels(this Channel channel)
        {
            var knownChannels = new HashSet<Channel>();
            VisitRecipients(channel, knownChannels);
            return knownChannels;
        }

        static void VisitRecipients(Channel channel, HashSet<Channel> knownChannels )
        {
            if (channel == null) return;

            knownChannels.Add(channel);
            foreach (var item in channel.Observers)
            {
                if (!knownChannels.Contains(item))
                    VisitRecipients(item, knownChannels);
            }
        }

    }
}
