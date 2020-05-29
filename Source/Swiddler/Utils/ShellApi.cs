using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Swiddler.Utils
{
    public static class ShellApi
    {
        private const int SHGFI_SMALLICON = 0x1;
        private const int SHGFI_LARGEICON = 0x0;
        private const int SHIL_JUMBO = 0x4;
        private const int SHIL_EXTRALARGE = 0x2;
        private const int MAX_PATH = 260;

        public enum IconSizeEnum
        {
            Small16 = SHGFI_SMALLICON,
            Medium32 = SHGFI_LARGEICON,
            Large48 = SHIL_EXTRALARGE,
            Huge256 = SHIL_JUMBO
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        static extern bool PathFindOnPath([In, Out] StringBuilder pszFile, [In] string[] ppszOtherDirs);

        [DllImport("user32")]
        private static extern
            IntPtr SendMessage(
            IntPtr handle,
            int Msg,
            IntPtr wParam,
            IntPtr lParam);


        [DllImport("shell32.dll")]
        private static extern int SHGetImageList(
            int iImageList,
            ref Guid riid,
            out Shell.IImageList ppv);

        [DllImport("Shell32.dll")]
        private static extern int SHGetFileInfo(
            string pszPath, 
            int dwFileAttributes, 
            ref Shell.SHFILEINFO psfi, 
            int cbFileInfo, 
            uint uFlags);

        [DllImport("user32")]
        private static extern int DestroyIcon(IntPtr hIcon);

        public static System.Drawing.Bitmap GetBitmapFromFolderPath(
            string filepath, IconSizeEnum iconsize)
        {
            IntPtr hIcon = GetIconHandleFromFolderPath(filepath, iconsize);
            return GetBitmapFromIconHandle(hIcon);
        }

        public static System.Drawing.Bitmap GetBitmapFromFilePath(
            string filepath, IconSizeEnum iconsize)
        {
            IntPtr hIcon = GetIconHandleFromFilePath(filepath, iconsize);
            return GetBitmapFromIconHandle(hIcon);
        }

        public static System.Drawing.Bitmap GetBitmapFromPath(
            string filepath, IconSizeEnum iconsize)
        {
            IntPtr hIcon = IntPtr.Zero;
            if (Directory.Exists(filepath))
            {
                hIcon = GetIconHandleFromFolderPath(filepath, iconsize);
            }
            else
            {
                if (File.Exists(filepath))
                {
                    hIcon = GetIconHandleFromFilePath(filepath, iconsize);
                }
            }
            return GetBitmapFromIconHandle(hIcon);
        }

        private static System.Drawing.Bitmap GetBitmapFromIconHandle(IntPtr hIcon)
        {
            if (hIcon == IntPtr.Zero)
                return null;

            using (var myIcon = System.Drawing.Icon.FromHandle(hIcon))
            {
                try
                {
                    return myIcon.ToBitmap();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        private static IntPtr GetIconHandleFromFilePath(string filepath, IconSizeEnum iconsize)
        {
            var shinfo = new Shell.SHFILEINFO();
            const uint SHGFI_SYSICONINDEX = 0x4000;
            const int FILE_ATTRIBUTE_NORMAL = 0x80;
            uint flags = SHGFI_SYSICONINDEX;
            return GetIconHandleFromFilePathWithFlags(filepath, iconsize, ref shinfo, FILE_ATTRIBUTE_NORMAL, flags);
        }

        private static IntPtr GetIconHandleFromFolderPath(string folderpath, IconSizeEnum iconsize)
        {
            var shinfo = new Shell.SHFILEINFO();

            const uint SHGFI_ICON = 0x000000100;
            const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
            const int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;
            return GetIconHandleFromFilePathWithFlags(folderpath, iconsize, ref shinfo, FILE_ATTRIBUTE_DIRECTORY, flags);
        }

        private static IntPtr GetIconHandleFromFilePathWithFlags(
            string filepath, IconSizeEnum iconsize,
            ref Shell.SHFILEINFO shinfo, int fileAttributeFlag, uint flags)
        {
            const int ILD_TRANSPARENT = 1;
            var retval = SHGetFileInfo(filepath, fileAttributeFlag, ref shinfo, Marshal.SizeOf(shinfo), flags);
            if (retval == 0) throw (new FileNotFoundException(filepath));
            var iconIndex = shinfo.iIcon;
            var iImageListGuid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
            Shell.IImageList iml;
            var hres = SHGetImageList((int)iconsize, ref iImageListGuid, out iml);
            var hIcon = IntPtr.Zero;
            hres = iml.GetIcon(iconIndex, ILD_TRANSPARENT, ref hIcon);
            return hIcon;
        }


        /// <summary>
        /// Gets the full path of the given executable filename as if the user had entered this
        /// executable in a shell. So, for example, the Windows PATH environment variable will
        /// be examined. If the filename can't be found by Windows, null is returned.</summary>
        /// <param name="exeName"></param>
        /// <returns>The full path if successful, or null otherwise.</returns>
        public static string ResolvePath(string exeName)
        {
            if (exeName.Length >= MAX_PATH)
                throw new ArgumentException($"The executable name '{exeName}' must have less than {MAX_PATH} characters.",
                    nameof(exeName));

            StringBuilder sb = new StringBuilder(exeName, MAX_PATH);
            return PathFindOnPath(sb, null) ? sb.ToString() : null;
        }

    }
}
