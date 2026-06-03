using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClaudeCommit.Services
{
    internal static class NativeMethods
    {
        // ── window enumeration ────────────────────────────────────────────────────

        internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        // ── focus / foreground ────────────────────────────────────────────────────

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        // ── window collection helper ──────────────────────────────────────────────

        internal static IntPtr[] GetProcessTopLevelWindows(int pid)
        {
            var result = new System.Collections.Generic.List<IntPtr>();
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out uint windowPid);
                if (windowPid == (uint)pid && IsWindowVisible(hwnd))
                    result.Add(hwnd);
                return true;
            }, IntPtr.Zero);
            return result.ToArray();
        }

        // ── keyboard simulation ───────────────────────────────────────────────────
        // keybd_event: simpler than SendInput struct layout, same effect for modifier+key combos

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYUP = 0x0002;

        internal static void SendCtrlA()
        {
            keybd_event((byte)Keys.ControlKey, 0, 0, UIntPtr.Zero);
            keybd_event((byte)Keys.A,           0, 0, UIntPtr.Zero);
            keybd_event((byte)Keys.A,           0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)Keys.ControlKey,  0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        internal static void SendCtrlV()
        {
            keybd_event((byte)Keys.ControlKey, 0, 0, UIntPtr.Zero);
            keybd_event((byte)Keys.V,           0, 0, UIntPtr.Zero);
            keybd_event((byte)Keys.V,           0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)Keys.ControlKey,  0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
