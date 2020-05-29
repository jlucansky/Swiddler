using System;
using System.Runtime.InteropServices;

namespace Swiddler.Utils.Shell
{
    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGEINFO
    {
        public IntPtr hbmImage;
        public IntPtr hbmMask;
        public int Unused1;
        public int Unused2;
        public Shell.RECT rcImage;
    }
}
