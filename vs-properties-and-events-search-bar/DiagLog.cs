using System;
using System.IO;

namespace PropertiesSearch
{
    /// <summary>
    /// Tiny always-on file log (%LOCALAPPDATA%\PropertiesSearch\log.txt) so the
    /// extension can be diagnosed without restarting VS with /log. Never throws.
    /// </summary>
    internal static class DiagLog
    {
        private static readonly string LogPath = InitPath();
        private static readonly object Sync = new object();

        private static string InitPath()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PropertiesSearch");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "log.txt");
            }
            catch { return null; }
        }

        internal static void Write(string message)
        {
            if (LogPath == null) return;
            try
            {
                lock (Sync)
                    File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}\r\n");
            }
            catch { /* diagnostics must never break the extension */ }
        }
    }
}
