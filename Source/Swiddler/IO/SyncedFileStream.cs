using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Swiddler.IO
{
    /// <summary>
    /// FileStream with cross-process locking mechanism
    /// </summary>
    public class SyncedFileStream : FileStream
    {
        public string PathHash { get; private set; }
        public Mutex Mutex { get; private set; }

        public bool DataAvailable => Position < Length;

        public event Action LastClosed; // notify handler when this is the last disposing stream for file

        readonly string mutexName;

        public SyncedFileStream(string path) : base(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)
        {
            using (var hashAlgorithm = SHA1.Create())
                PathHash = ByteToHexBitFiddle(hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(Name.ToLower())), 10);

            mutexName = "1F6B1B5E9B734F76881." + PathHash;
            Mutex = new Mutex(false, mutexName);
        }

        public void Lock()
        {
            try
            {
                Mutex.WaitOne();
            }
            catch (AbandonedMutexException) { }
        }

        public void Unlock()
        {
            Mutex.ReleaseMutex();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var mutexCopy = Mutex;
                Mutex = null;

                if (mutexCopy != null)
                {
                    mutexCopy.Dispose();
                }
            }

            base.Dispose(disposing);


            if (disposing && LastClosed != null)
            {
                if (Mutex.TryOpenExisting(mutexName, out Mutex tmp))
                    tmp.Dispose();
                else
                    LastClosed(); // raise event after stream was closed
            }
        }

        static string ByteToHexBitFiddle(byte[] bytes, int count)
        {
            char[] c = new char[count * 2];
            int b;
            for (int i = 0; i < count; i++)
            {
                b = bytes[i] >> 4;
                c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = bytes[i] & 0xF;
                c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }
            return new string(c);
        }
    }
}
