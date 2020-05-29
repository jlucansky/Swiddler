using Swiddler.Properties;
using Swiddler.Utils;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Swiddler.Common
{
    public class Injector
    {
        const int PROCESS_QUERY_INFORMATION = 0x400;
        const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        const int SYNCHRONIZE = 0x00100000;
        const int WAIT_TIMEOUT = 0x00000102;

        public int ProcessId { get; }

        public int WSVersionRequired { get; set; } = 0x202;

        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);


        [DllImport("kernel32", SetLastError = true)] static extern int IsWow64Process(IntPtr hProcess, out bool bWow64Process);
        [DllImport("kernel32", SetLastError = true)] static extern IntPtr OpenProcess(int Access, bool InheritHandle, int ProcessId);
        [DllImport("kernel32.dll", SetLastError = true)] static extern int WaitForSingleObject(IntPtr hHandle, int dwMilliseconds);
        [DllImport("kernel32")] static extern void CloseHandle(IntPtr hProcess);

        public Injector(int processId)
        {
            ProcessId = processId;
        }

        public string GetRunDllPath()
        {
            string system32 = "System32"; // if we have matching bitness, use default

            if (Environment.Is64BitOperatingSystem)
            {
                if (Environment.Is64BitProcess)
                {
                    if (GetTargetProcessBitness() == 32)
                        system32 = "SysWoW64"; // target process is 32-bit but current process is 64-bit
                }
                else
                {
                    if (GetTargetProcessBitness() == 64)
                        system32 = "Sysnative"; // target process is 64-bit but current process is 32-bit
                }
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), system32, "rundll32.exe");
        }

        int _TargetProcessBitness = 0;
        public int GetTargetProcessBitness()
        {
            if (_TargetProcessBitness == 0)
            {
                if (Environment.Is64BitOperatingSystem)
                    _TargetProcessBitness = IsWow64Process(ProcessId) ? 32 : 64;
                else
                    _TargetProcessBitness = 32;
            }
            return _TargetProcessBitness;
        }

        /// <summary>
        /// TRUE if the process is running under WOW64 on an Intel64 or x64 processor. 
        /// If the process is running under 32-bit Windows, the value is set to FALSE. 
        /// If the process is a 32-bit application running under 64-bit Windows 10 on ARM, the value is set to FALSE. 
        /// If the process is a 64-bit application running under 64-bit Windows, the value is also set to FALSE
        /// </summary>
        public static bool IsWow64Process(int processId)
        {
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION, false, processId);

            try
            {
                if (hProcess == IntPtr.Zero)
                {
                    hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                    if (hProcess == IntPtr.Zero)
                        throw new Win32Exception();
                }

                if (IsWow64Process(hProcess, out var result) != 0)
                    return result;
                else
                    throw new Win32Exception();
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                    CloseHandle(hProcess);
            }

        }

        string ExtractLib(EnvironmentVariableTarget scope, string filename, Func<byte[]> content)
        {
            string temp = Environment.GetEnvironmentVariable("TEMP", scope);
            string path = Path.Combine(temp, nameof(Swiddler), GetVersion(), filename);

            if (!File.Exists(path))
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllBytes(path, content());
            }

            return path;
        }

        public string GetExtractedLibPath()
        {
            int bitness = GetTargetProcessBitness();

            string filename;

            if (bitness == 32) filename = nameof(Resources.netprobe32);
            else if (bitness == 64) filename = nameof(Resources.netprobe64);
            else throw new ArgumentOutOfRangeException(nameof(bitness));

            filename += ".dll";

            string path = Path.Combine(GetExecutingDirectoryName(), filename);

            if (File.Exists(path))
                return path;

            byte[] content()
            {
                if (bitness == 32) return Resources.netprobe32;
                else if (bitness == 64) return Resources.netprobe64;
                else throw new ArgumentOutOfRangeException(nameof(bitness));
            }

            try
            {
                return ExtractLib(EnvironmentVariableTarget.Machine, filename, content);
            }
            catch (UnauthorizedAccessException)
            {
                // fallback to user's temp
                return ExtractLib(EnvironmentVariableTarget.User, filename, content);
            }
        }

        static string GetExecutingDirectoryName()
        {
            var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
            return new FileInfo(location.LocalPath).Directory.FullName;
        }

        string GetVersion()
        {
            return GetType().Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().Single().InformationalVersion.ToString(CultureInfo.InvariantCulture);
        }

        public void Inject(int monitorPort)
        {
            EnsureProcessRunning();

            using (var process = new Process())
            {
                process.StartInfo.FileName = GetRunDllPath();
                process.StartInfo.Arguments = $@"""{GetExtractedLibPath()}"",#101 {ProcessId} {monitorPort} {WSVersionRequired}";

                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                    return; // success

                if (process.ExitCode < 0)
                    throw new Exception($"Process injection failed ({process.ExitCode})");
                else
                    throw new Win32Exception(process.ExitCode);
            }
        }

        /// <summary>
        /// Returns connection to injected process.
        /// </summary>
        public TcpClient InjectAndConnect()
        {
            TcpListener listener = null;
            try
            {
                listener = Net.CreateNewListener(IPAddress.Loopback, ConnectTimeout);

                Inject(((IPEndPoint)listener.LocalEndpoint).Port);

                return listener.AcceptTcpClient(); // return first accepted client, then stop listener immediately
            }
            finally
            {
                listener?.Stop();
            }
        }

        void EnsureProcessRunning()
        {
            IntPtr hProcess = OpenProcess(SYNCHRONIZE, false, ProcessId);

            if (hProcess == IntPtr.Zero)
                throw new ProcessNotFoundException(ProcessId);

            try
            {
                if (WaitForSingleObject(hProcess, 0) != WAIT_TIMEOUT)
                    throw new ProcessNotFoundException(ProcessId);
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

    }

    public class ProcessNotFoundException: Exception
    {
        public ProcessNotFoundException(int ProcessId) : base("Unable to find process with ID " + ProcessId)
        {

        }
    }

}

