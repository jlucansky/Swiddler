
using System;
using System.Runtime.InteropServices;

namespace Swiddler.Utils
{
    public class TaskbarProgress
    {
        private readonly IntPtr _handle;
        private readonly ITaskbarList3 _taskbarList;

        private bool _isVisible = false;
        private double? _progressValue = null;

        public TaskbarProgress(IntPtr hwnd)
        {
            _handle = hwnd;
            try { _taskbarList = (ITaskbarList3)new TaskbarInstance(); } catch { }
        }

        private enum TaskbarStates
        {
            NoProgress = 0,
            Indeterminate = 0x1,
            Normal = 0x2,
            Error = 0x4,
            Paused = 0x8
        }

        [ComImport]
        [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            // ITaskbarList
            [PreserveSig]
            void HrInit();
            [PreserveSig]
            void AddTab(IntPtr hwnd);
            [PreserveSig]
            void DeleteTab(IntPtr hwnd);
            [PreserveSig]
            void ActivateTab(IntPtr hwnd);
            [PreserveSig]
            void SetActiveAlt(IntPtr hwnd);

            // ITaskbarList2
            [PreserveSig]
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

            // ITaskbarList3
            [PreserveSig]
            void SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);
            [PreserveSig]
            void SetProgressState(IntPtr hwnd, TaskbarStates state);
        }

        [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
        [ClassInterface(ClassInterfaceType.None)]
        [ComImport]
        private class TaskbarInstance
        {
        }

        void Hide()
        {
            _isVisible = false;
            _taskbarList?.SetProgressState(_handle, TaskbarStates.NoProgress);
        }

        void Show()
        {
            _isVisible = true;
            _taskbarList?.SetProgressState(_handle, ProgressValue.HasValue ? TaskbarStates.Normal : TaskbarStates.Indeterminate);
            if (ProgressValue.HasValue)
                _taskbarList?.SetProgressValue(_handle, (ulong)(ProgressValue.Value * 100), 100);
        }

        public bool Visible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                    if (value) Show(); else Hide();
            }
        }

        public double? ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                if (value.HasValue)
                {
                    _taskbarList?.SetProgressState(_handle, _isVisible ? TaskbarStates.Normal : TaskbarStates.NoProgress);
                    if (_isVisible)
                        _taskbarList?.SetProgressValue(_handle, (ulong)(value.Value * 100), 100);
                }
                else
                {
                    _taskbarList?.SetProgressState(_handle, _isVisible ? TaskbarStates.Indeterminate : TaskbarStates.NoProgress);
                }
            }
        }
    }
}