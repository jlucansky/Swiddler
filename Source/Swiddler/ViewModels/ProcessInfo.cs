using Swiddler.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using static Swiddler.Utils.ShellApi;

namespace Swiddler.ViewModels
{
    public class ProcessInfo
    {
        public static IconSizeEnum DefaultIconSize { get; set; } = IconSizeEnum.Small16;


        public int ProcessId { get; private set; }
        public string Name { get; private set; }
        public string Path { get; private set; }
        public ImageSource Icon { get; private set; }


        static readonly Dictionary<string, ImageSource> IconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        static readonly string DefaultExePath = ResolvePath("smss.exe");

        private ProcessInfo()
        {
        }

        public static ProcessInfo Get(int? processId)
        {
            if ((processId ?? 0) == 0)
                return null;

            using (var searcher = new ManagementObjectSearcher($"Select * From Win32_Process Where ProcessID={processId}"))
            {
                var obj = searcher.Get().Cast<ManagementObject>().SingleOrDefault();

                if (obj == null)
                    return null;

                var pi = new ProcessInfo()
                {
                    ProcessId = processId.Value,
                    Name = (string)obj["Name"],
                    Path = (string)obj["ExecutablePath"],
                };

                pi.ResolveIcon();

                return pi;
            }
        }

        static readonly Dispatcher Dispatcher = Application.Current.Dispatcher;

        private void ResolveIcon()
        {
            string iconKey = Path;

            if (string.IsNullOrEmpty(iconKey))
                iconKey = DefaultExePath;

            lock (IconCache)
            {
                if (IconCache.TryGetValue(iconKey, out var img))
                {
                    Icon = img;
                    return;
                }
                else
                {
                    using (var bitmap = GetBitmapFromFilePath(iconKey, DefaultIconSize))
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            Icon = bitmap.ImageSourceForBitmap();
                            IconCache[iconKey] = Icon;
                        }));
                    }
                }
            }
        }

        public override string ToString()
        {
            return Name;
        }

    }
}
