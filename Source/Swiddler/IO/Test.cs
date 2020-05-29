using Swiddler.Common;
using Swiddler.DataChunks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace Swiddler.IO
{
#if DEBUG

    class Test
    {
        public static void Run()
        {
            WriteSmall();
            //Write();
            //Read();
        }

        static void Write()
        {
            using (var stream = new FileStream("test_32", FileMode.Create, FileAccess.ReadWrite, FileShare.Read, Constants.BlockSize/* , FileOptions.DeleteOnClose */ ))
            {
                var writer = new BlockWriter(stream);

                long seq = 0;

                for (int i = 800; i < 1000; i++)
                {
                    for (int n = 0; n < 1000; n++)
                    {
                        var packet = new Packet()
                        {
                            Flow = TrafficFlow.Inbound,
                            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 123),
                            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 456),
                            SequenceNumber = seq++,
                            Payload = CreatePayload(i * 1),
                        };

                        writer.Write(packet);
                    }

                }



            }
        }

        static void WriteSmall()
        {
            using (var stream = new FileStream("test_s", FileMode.Create, FileAccess.ReadWrite, FileShare.Read, Constants.BlockSize/* , FileOptions.DeleteOnClose */ ))
            {
                var writer = new BlockWriter(stream);

                long seq = 0;

                for (int i = 800; i < 850; i++)
                {
                    for (int c = 0; c < 1; c++)
                    {
                        var packet = new Packet()
                        {
                            Flow = i % 2 == 0 ? TrafficFlow.Inbound : TrafficFlow.Outbound,
                            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 123),
                            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 456),
                            SequenceNumber = seq++,
                            Payload = new byte[i],
                        };

                        var data = packet.Payload;

                        for (int n = 0; n < data.Length; n++)
                            data[n] = (byte)((n + 32) % 192);

                        writer.Write(packet);
                    }
                }



            }
        }

        static void Read()
        {
            using (var stream = new FileStream("75268096", FileMode.Open, FileAccess.Read, FileShare.Read, Constants.BlockSize/* , FileOptions.DeleteOnClose */ ))
            {
                var reader = new BlockReader(stream);
                reader.Position = 75268096;

                //reader.Position = 105810828;
                //reader.Position = 105810830;

                var list = new List<Packet>();

                var watch = Stopwatch.StartNew();
                while (reader.Read())
                {
                    //list.Add(reader.Packet);
                }
                watch.Stop();
                Console.WriteLine(watch.ElapsedMilliseconds);

                watch.Restart();

                int skip = 0;

                var fileSize = stream.Length;
                for (long pos = fileSize / 5 * 4; pos < fileSize; pos++)
                {
                    reader.Seek(pos);
                    reader.Read();


                    skip += 1791;
                    if (skip > 10000) skip = 0;
                    pos += skip;
                }

                watch.Stop();
                Console.WriteLine(watch.ElapsedMilliseconds);

            }

        }

        static byte[] CreatePayload(int seed)
        {
            var data = new byte[seed];

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(seed % 256);
            }

            return data;
        }
    }

#endif
}
