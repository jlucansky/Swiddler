using Swiddler.Common;
using Swiddler.DataChunks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Swiddler.Channels
{
    public class RewriteRule
    {
        public byte[] Search { get; set; }
        public byte[] Replace { get; set; }
        public TrafficFlow Flow { get; set; }
    }

    public class RewriteChannel : Channel
    {
        readonly List<RewriteRule> inboundRules = new List<RewriteRule>();
        readonly List<RewriteRule> outboundRules = new List<RewriteRule>();

        public RewriteChannel(Session session) : base(session) 
        {
            foreach (var item in session.Rewrites)
            {
                AddRule(item);
            }
        }

        protected override void OnReceiveNotification(Packet packet)
        {
            switch (packet.Flow)
            {
                case TrafficFlow.Inbound:
                    ReplaceBytes(packet, inboundRules);
                    break;
                case TrafficFlow.Outbound:
                    ReplaceBytes(packet, outboundRules);
                    break;
                default:
                    throw new InvalidOperationException("Invalid flow: " + packet.Flow);
            }
        }

        public void AddRule(RewriteRule rule)
        {
            if (rule.Flow == TrafficFlow.Inbound) inboundRules.Add(rule);
            if (rule.Flow == TrafficFlow.Outbound) outboundRules.Add(rule);
        }

        static void ReplaceBytes(Packet packet, List<RewriteRule> rules)
        {
            if (rules.Count > 0)
            {
                packet.Payload = ReplaceBytes(packet.Payload, rules).ToArray();
            }
        }

        // https://stackoverflow.com/questions/5132890/c-sharp-replace-bytes-in-byte
        static IEnumerable<byte> ReplaceBytes(IEnumerable<byte> source, IEnumerable<RewriteRule> replacements)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (replacements == null) throw new ArgumentNullException(nameof(replacements));
            if (replacements.Any(r => r.Search == null || r.Search.Length == 0)) throw new ArgumentOutOfRangeException(nameof(replacements), "Search parameter cannot be null or empty");
            if (replacements.Any(r => r.Replace == null)) throw new ArgumentOutOfRangeException(nameof(replacements), "Replace parameter cannot be null");

            var maxMatchSize = replacements.Select(r => r.Search.Length).Max();
            var bufferSize = maxMatchSize * 2;
            var buffer = new byte[bufferSize];
            int bufferStart = 0;
            int bufferPosition = 0;

            byte[] nextBytes()
            {
                foreach (var rule in replacements)
                {
                    if (ByteStartsWith(buffer, bufferStart, bufferPosition - bufferStart, rule.Search))
                    {
                        bufferStart += rule.Search.Length;
                        return rule.Replace;
                    }
                }

                var returnBytes = new byte[] { buffer[bufferStart] };
                bufferStart++;
                return returnBytes;
            }

            foreach (var dataByte in source)
            {
                buffer[bufferPosition] = dataByte;
                bufferPosition++;

                if (bufferPosition - bufferStart >= maxMatchSize)
                {
                    foreach (var resultByte in nextBytes())
                        yield return resultByte;
                }

                if (bufferPosition == bufferSize - 1)
                {
                    Buffer.BlockCopy(buffer, bufferStart, buffer, 0, bufferPosition - bufferStart);
                    bufferPosition -= bufferStart;
                    bufferStart = 0;
                }
            }

            while (bufferStart < bufferPosition)
            {
                foreach (var resultByte in nextBytes())
                    yield return resultByte;
            }
        }

        static bool ByteStartsWith(byte[] data, int dataOffset, int dataLength, byte[] startsWith)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (startsWith == null) throw new ArgumentNullException(nameof(startsWith));
            if (dataLength < startsWith.Length) return false;

            for (int i = 0; i < startsWith.Length; i++)
            {
                if (data[i + dataOffset] != startsWith[i])
                    return false;
            }

            return true;
        }

    }
}
