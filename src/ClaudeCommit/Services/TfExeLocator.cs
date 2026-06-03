using System;
using System.IO;

namespace ClaudeCommit.Services
{
    /// <summary>
    /// Finds TF.exe from the VS IDE directory supplied at package init time (main-thread safe).
    /// Result is cached after first lookup so background threads never need to call IVsShell.
    /// </summary>
    internal sealed class TfExeLocator
    {
        // VS IDE dir, e.g. "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\"
        private readonly string _vsIdeDir;

        // null  = not yet searched
        // ""    = searched, not found
        // path  = found
        private string _cached;

        public TfExeLocator(string vsIdeDir) => _vsIdeDir = vsIdeDir ?? string.Empty;

        /// <returns>Full path to TF.exe, or <c>null</c> if Team Explorer is not installed.</returns>
        public string FindTfExe()
        {
            if (_cached != null)
                return _cached.Length > 0 ? _cached : null;

            _cached = LocateCore() ?? string.Empty;
            return _cached.Length > 0 ? _cached : null;
        }

        private string LocateCore()
        {
            // 1. TF.exe next to the VS IDE (Team Explorer component)
            if (!string.IsNullOrEmpty(_vsIdeDir))
            {
                var candidate = Path.Combine(
                    _vsIdeDir,
                    "CommonExtensions", "Microsoft", "TeamFoundation",
                    "Team Explorer", "TF.exe");

                if (File.Exists(candidate))
                    return candidate;
            }

            // 2. tf / tf.exe somewhere on PATH
            try
            {
                var tfOnPath = FindOnPath("TF.exe") ?? FindOnPath("tf.exe") ?? FindOnPath("tf");
                if (tfOnPath != null) return tfOnPath;
            }
            catch { /* ignore PATH errors */ }

            return null;
        }

        private static string FindOnPath(string exeName)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    var full = Path.Combine(dir.Trim(), exeName);
                    if (File.Exists(full)) return full;
                }
                catch { /* skip malformed entries */ }
            }
            return null;
        }
    }
}
