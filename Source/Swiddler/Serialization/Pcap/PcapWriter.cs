using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
namespace Swiddler.Serialization.Pcap
{
    public delegate void ExceptionEventDelegate(object sender, Exception exception);

    public sealed class PcapWriter : Disposable
    {
        #region event & delegate
        public event ExceptionEventDelegate OnExceptionEvent;

        private void OnException(Exception exception)
        {
            ExceptionEventDelegate handler = OnExceptionEvent;
            if (handler != null)
                handler(this, exception);
            else
                ExceptionDispatchInfo.Capture(exception).Throw();
        }
        #endregion

        #region fields & properties
        private Stream stream;
        private BinaryWriter binaryWriter;
        private SectionHeader header = null;
        private object syncRoot = new object();
        #endregion

        #region ctor

        public PcapWriter(Stream stream, SectionHeader header)
        {
            Initialize(stream, header);
        }

         private void Initialize(Stream stream, SectionHeader header)
         {                     
             this.header = header;              
             this.stream = stream;
             binaryWriter = new BinaryWriter(stream, System.Text.Encoding.Default, leaveOpen: true);
             binaryWriter.Write(header.ConvertToByte());            
         }
        #endregion

         /// <summary>
         /// Close stream, dispose members
         /// </summary>
        public void Close()
        {
            Dispose();
        }

        public void WritePacket(PcapPacket packet)
        {
            try
            {
                uint secs = (uint)packet.Seconds;
                uint usecs = (uint)packet.Microseconds;
                if (header.NanoSecondResolution)
                    usecs *= 1000;
                uint caplen = (uint)packet.Data.Length;
                uint len = (uint)packet.Data.Length;
                byte[] data = packet.Data;

                List<byte> ret = new List<byte>();

                ret.AddRange(BitConverter.GetBytes(secs.ReverseByteOrder(header.ReverseByteOrder)));
                ret.AddRange(BitConverter.GetBytes(usecs.ReverseByteOrder(header.ReverseByteOrder)));
                ret.AddRange(BitConverter.GetBytes(caplen.ReverseByteOrder(header.ReverseByteOrder)));
                ret.AddRange(BitConverter.GetBytes(len.ReverseByteOrder(header.ReverseByteOrder)));
                ret.AddRange(data);
                if (ret.Count > header.MaximumCaptureLength)
                    throw new ArgumentOutOfRangeException(string.Format("[PcapWriter.WritePacket] packet length: {0} is greater than MaximumCaptureLength: {1}", ret.Count, header.MaximumCaptureLength));
                lock (syncRoot)
                {
                    binaryWriter?.Write(ret.ToArray());
                }
            }
            catch (Exception exc)
            {
                OnException(exc);
            }
        }         


        #region IDisposable Members
        /// <summary>
        /// Close stream, dispose members
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (binaryWriter != null)
            {
                binaryWriter.Flush();
                binaryWriter.Dispose();
                binaryWriter = null;
                stream.Flush();
            }
        }

        #endregion      
    }  
}
