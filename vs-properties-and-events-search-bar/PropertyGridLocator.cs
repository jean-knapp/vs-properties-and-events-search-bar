using Microsoft.VisualStudio.Shell;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PropertiesSearch
{
    /// <summary>
    /// Finds the live PropertyGrid inside the Properties tool window. The shell
    /// hosts a managed PropertyGrid subclass (Microsoft.VisualStudio.PropertyBrowser),
    /// so enumerating our own process's windows and asking WinForms for the managed
    /// control behind each HWND is enough — no undocumented frame properties needed.
    /// </summary>
    internal static class PropertyGridLocator
    {
        internal static PropertyGrid FindPropertyBrowser()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            PropertyGrid found = null;
            int ourPid = Process.GetCurrentProcess().Id;

            // Top-level windows of this process (the main window plus any floating
            // tool windows), then every descendant of each.
            EnumWindows((hwnd, lparam) =>
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid != ourPid) return true;

                if (Match(hwnd) is PropertyGrid pg) { found = pg; return false; }

                EnumChildWindows(hwnd, (child, l) =>
                {
                    if (Match(child) is PropertyGrid cpg) { found = cpg; return false; }
                    return true;
                }, IntPtr.Zero);

                return found == null;
            }, IntPtr.Zero);

            return found;
        }

        private static PropertyGrid Match(IntPtr hwnd)
        {
            if (!(Control.FromHandle(hwnd) is PropertyGrid grid)) return null;

            // The shell's property browser lives in a Microsoft.VisualStudio.*
            // namespace; requiring that avoids grabbing some other extension's grid.
            string name = grid.GetType().FullName ?? string.Empty;
            if (name.StartsWith("Microsoft.VisualStudio.", StringComparison.OrdinalIgnoreCase))
                return grid;

            DiagLog.Write($"Rejected PropertyGrid candidate: {name}");
            return null;
        }

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lparam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lparam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lparam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    }
}
