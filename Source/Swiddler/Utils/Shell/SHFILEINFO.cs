using System;
using System.Runtime.InteropServices;

namespace Swiddler.Utils.Shell
{
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 254)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szTypeName;
    };
}
