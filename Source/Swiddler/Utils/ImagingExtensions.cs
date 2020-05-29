using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Swiddler.Utils
{
    public static class ImagingExtensions
    {

        [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool DeleteObject([In] IntPtr hObject);

        public static ImageSource ImageSourceForBitmap(this Bitmap bmp) // call from UI thread !
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(handle);
            }
        }

    }
}
