using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ClaudeCommit.Services
{
    internal sealed class VcsDetectorService : IVcsDetector
    {
        private readonly AsyncPackage _package;
        private readonly TfExeLocator _tfExeLocator;

        // Timeout for tf.exe workfold — local workspaces respond instantly;
        // server workspaces can be slow, so cap at 4 seconds.
        private static readonly TimeSpan TfvcDetectTimeout = TimeSpan.FromSeconds(4);

        public VcsDetectorService(AsyncPackage package, TfExeLocator tfExeLocator)
        {
            _package      = package;
            _tfExeLocator = tfExeLocator;
        }

        public async Task<VcsType> DetectAsync(CancellationToken cancellationToken)
        {
            var solutionDir = await GetSolutionDirAsync(cancellationToken);
            if (string.IsNullOrEmpty(solutionDir))
                return VcsType.Unknown;

            // Git check is purely local (directory walk) — fast, no I/O beyond stat calls
            if (HasGitRepo(solutionDir))
                return VcsType.Git;

            // TFVC check via tf.exe workfold — only attempted if Team Explorer is installed
            var tfExe = _tfExeLocator.FindTfExe();
            if (tfExe != null && await IsTfvcWorkspaceAsync(tfExe, solutionDir, cancellationToken))
                return VcsType.Tfvc;

            return VcsType.Unknown;
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        private async Task<string> GetSolutionDirAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var solution = await _package.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            string solutionDir = null;
            solution?.GetSolutionInfo(out solutionDir, out _, out _);
            return solutionDir;
        }

        private static bool HasGitRepo(string solutionDir)
        {
            var dir = new DirectoryInfo(solutionDir);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return true;
                dir = dir.Parent;
            }
            return false;
        }

        private static async Task<bool> IsTfvcWorkspaceAsync(
            string tfExe,
            string solutionDir,
            CancellationToken cancellationToken)
        {
            // Link a short timeout so a slow server workspace doesn't block the user
            using (var timeoutCts = new CancellationTokenSource(TfvcDetectTimeout))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token))
            {
                try
                {
                    using (var process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo
                        {
                            FileName               = tfExe,
                            Arguments              = $"workfold \"{solutionDir}\"",
                            UseShellExecute        = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError  = true,
                            CreateNoWindow         = true,
                        };
                        process.Start();

                        // Drain pipes concurrently to avoid pipe-buffer deadlock
                        var outTask = process.StandardOutput.ReadToEndAsync();
                        var errTask = process.StandardError.ReadToEndAsync();

                        await process.WaitForExitAsync(linked.Token);
                        await outTask;
                        await errTask;

                        return process.ExitCode == 0;
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timed out — assume not TFVC (or server workspace too slow to be useful)
                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
