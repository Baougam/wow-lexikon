using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WowLexikon
{
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr64(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr64(IntPtr h, int i, IntPtr v);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong32(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] static extern int SetWindowLong32(IntPtr h, int i, int v);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);

        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x80;
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        public static bool IsDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        public static System.Drawing.Point Cursor()
        {
            POINT p; GetCursorPos(out p);
            return new System.Drawing.Point(p.X, p.Y);
        }

        static long GetEx(IntPtr h) { return IntPtr.Size == 8 ? GetWindowLongPtr64(h, GWL_EXSTYLE).ToInt64() : GetWindowLong32(h, GWL_EXSTYLE); }
        static void SetEx(IntPtr h, long v) { if (IntPtr.Size == 8) SetWindowLongPtr64(h, GWL_EXSTYLE, new IntPtr(v)); else SetWindowLong32(h, GWL_EXSTYLE, (int)v); }

        /// <summary>Fenster nimmt keinen Fokus (WoW bleibt aktiv) und erscheint nicht in Alt+Tab.</summary>
        public static void SetNoActivate(IntPtr h, bool on)
        {
            long ex = GetEx(h);
            ex = on ? (ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW) : (ex & ~WS_EX_NOACTIVATE);
            SetEx(h, ex);
        }

        public static System.Drawing.Rectangle WindowRect(IntPtr h)
        {
            RECT r; GetWindowRect(h, out r);
            return System.Drawing.Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }

        public static string ForegroundProcessName()
        {
            try
            {
                uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                using (var p = Process.GetProcessById((int)pid)) return p.ProcessName;
            }
            catch { return ""; }
        }

        public static bool IsWowForeground()
        {
            string n = ForegroundProcessName().ToLowerInvariant();
            return n.StartsWith("wow") || n.Contains("warcraft");
        }
    }
}
