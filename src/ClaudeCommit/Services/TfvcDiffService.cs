using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ClaudeCommit.Services
{
    internal sealed class TfvcDiffService : IVcsDiffService
    {
        // TFVC diffs can be huge (many files, binary indicators, CRLF expansion).
        // Cap at 48 k chars — well within Claude's context window even with the prompt wrapper.
        private const int MaxDiffChars = 48_000;

        private readonly AsyncPackage _package;
        private readonly TfExeLocator _tfExeLocator;

        public TfvcDiffService(AsyncPackage package, TfExeLocator tfExeLocator)
        {
            _package      = package;
            _tfExeLocator = tfExeLocator;
        }

        public async Task<DiffResult> GetDiffAsync(CancellationToken cancellationToken)
        {
            var tfExe = _tfExeLocator.FindTfExe();
            if (tfExe == null) return DiffResult.Empty;

            var solutionDir = await GetSolutionDirAsync(cancellationToken);
            if (string.IsNullOrEmpty(solutionDir)) return DiffResult.Empty;

            // Run tf.exe commands on thread-pool to keep VS UI thread free
            return await Task.Run(async () =>
            {
                // Step 1: pending-change summary
                var rawStatus = await RunTfAsync(
                    tfExe,
                    $"status /recursive /noprompt /format:brief \"{solutionDir}\"",
                    cancellationToken);

                var status = FilterStatusLines(rawStatus);
                if (string.IsNullOrWhiteSpace(status))
                    return DiffResult.Empty; // nothing pending

                // Step 2: unified diff of all pending changes
                var diff = (await RunTfAsync(
                    tfExe,
                    $"diff /noprompt /format:unified /recursive \"{solutionDir}\"",
                    cancellationToken)).Trim();

                if (diff.Length > MaxDiffChars)
                    diff = diff.Substring(0, MaxDiffChars) + "\n[... diff truncated — too large ...]";

                return new DiffResult(
                    statusSummary: status,
                    diffContent:   diff,
                    hasChanges:    true,
                    vcsType:       VcsType.Tfvc);

            }, cancellationToken);
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Strips tf.exe header lines (Collection:, Workspace:, "no pending changes") so only
        /// the actual file entries reach the prompt.
        /// </summary>
        private static string FilterStatusLines(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var meaningful = raw
                .Split('\n')
                .Select(l => l.TrimEnd())
                .Where(l => !string.IsNullOrWhiteSpace(l)
                         && !l.StartsWith("Collection:", StringComparison.OrdinalIgnoreCase)
                         && !l.StartsWith("Workspace:", StringComparison.OrdinalIgnoreCase)
                         && l.IndexOf("no pending changes", StringComparison.OrdinalIgnoreCase) < 0)
                .ToArray();

            return string.Join(Environment.NewLine, meaningful);
        }

        private async Task<string> GetSolutionDirAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var solution = await _package.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            string solutionDir = null;
            solution?.GetSolutionInfo(out solutionDir, out _, out _);
            return solutionDir;
        }

        private static async Task<string> RunTfAsync(
            string tfExe,
            string arguments,
            CancellationToken cancellationToken)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName               = tfExe,
                    Arguments              = arguments,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };

                process.Start();

                // Drain both pipes concurrently — not reading stderr deadlocks when its buffer fills
                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(cancellationToken);

                var output = await outTask;
                await errTask; // discard stderr

                return output;
            }
        }
    }
}
