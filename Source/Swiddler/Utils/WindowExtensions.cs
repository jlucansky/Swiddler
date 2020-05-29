using Swiddler.MarkupExtensions;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Shell;

namespace Swiddler.Utils
{
    internal static class WindowExtensions
    {
        [DllImport("user32.dll")] private extern static int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")] private extern static int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        const int GWL_STYLE = -16;
        const int GWL_EXSTYLE = -20;

        const int WS_MAXIMIZEBOX = 0x00010000;
        const int WS_MINIMIZEBOX = 0x00020000;
        const int WS_SYSMENU = 0x00080000;
        const int WS_CAPTION = 0x00C00000;
        const int WS_EX_DLGMODALFRAME = 0x0001;
        const uint WM_SETICON = 0x0080;

        const int SWP_NOSIZE = 0x0001;
        const int SWP_NOMOVE = 0x0002;
        const int SWP_NOZORDER = 0x0004;
        const int SWP_FRAMECHANGED = 0x0020;

        public static void RemoveIcon(this Window window)
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;

            // Change the extended window style to not show a window icon
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_DLGMODALFRAME);
            
            // Update the window's non-client area to reflect the changes
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            
            SendMessage(hwnd, WM_SETICON, new IntPtr(1), IntPtr.Zero);
            SendMessage(hwnd, WM_SETICON, IntPtr.Zero, IntPtr.Zero);
        }

        public static void DisableWindowControls(this Window window)
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~(WS_MAXIMIZEBOX | WS_MINIMIZEBOX | WS_SYSMENU | WS_CAPTION));
        }

        public static void DisableMinMaxControls(this Window window)
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~(WS_MAXIMIZEBOX | WS_MINIMIZEBOX));
        }

        // add shadow to window dispatched to prevent glitch when there is a visible shadow in short time with transparent window 
        public static void AddShadow(this Window window, bool dispatched)
        {
            void AddShadowCore()
            {
                var copy = (WindowChrome)WindowChrome.GetWindowChrome(window).Clone();
                copy.GlassFrameThickness = ShadowGlassFrameThickness.GetThickness();
                copy.Freeze();
                WindowChrome.SetWindowChrome(window, copy);
            }

            if (dispatched)
                Application.Current.Dispatcher.BeginInvoke(new Action(AddShadowCore));
            else
                AddShadowCore();
        }

        public static void CreateNativeWindow(this Control owner, Action<HwndSource> showNativeDialog, Point location)
        {
            var screenLoc = owner.PointToScreen(location);

            var wnd = new Window()
            {
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                Left = screenLoc.X,
                Top = screenLoc.Y,
                Width = 0,
                Height = 0,
                Owner = Window.GetWindow(owner),
            };

            wnd.SourceInitialized += (s, e) =>
            {
                showNativeDialog((HwndSource)PresentationSource.FromDependencyObject(wnd));
                wnd.Close();
            };
            wnd.ShowDialog();

            wnd.Close();
        }
        
        public static bool HasSize(this Rect rect)
        {
            return !rect.IsEmpty && rect.Width > 0 && rect.Height > 0;
        }
    }
}
