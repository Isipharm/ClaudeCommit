using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCommit.UI;
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

            // TryGetIncludedChangesAsync runs on main thread; null means VS API unavailable
            // (InfoBar warning already shown), empty list means user excluded everything.
            var includedChanges = await TryGetIncludedChangesAsync(cancellationToken);

            return await Task.Run(async () =>
            {
                if (includedChanges != null && includedChanges.Count == 0)
                    return DiffResult.Empty;

                return includedChanges != null
                    ? await BuildIncludedOnlyDiffAsync(tfExe, includedChanges, cancellationToken)
                    : await BuildAllChangesDiffAsync(tfExe, solutionDir, cancellationToken);

            }, cancellationToken);
        }

        // ── included-only path ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the list of included pending changes from the VS Pending Changes panel via
        /// VersionControlExt (late-bound dynamic to avoid a hard assembly reference).
        /// Returns null when the API is unavailable and shows an InfoBar warning so the caller
        /// can fall back to all pending changes.
        /// </summary>
        private async Task<IReadOnlyList<IncludedChange>> TryGetIncludedChangesAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            try
            {
                var dte = await _package.GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                dynamic vcExt = dte?.GetObject("Microsoft.VisualStudio.TeamFoundation.VersionControl.VersionControlExt");
                if (vcExt == null)
                {
                    await InfoBarHelper.ShowAsync(_package,
                        "Include filter unavailable — analyzing all pending changes.",
                        cancellationToken);
                    return null;
                }

                var result = new List<IncludedChange>();
                foreach (dynamic change in vcExt.PendingChanges.IncludedChanges)
                {
                    string localItem      = (string)change.LocalItem;
                    string changeTypeName = change.ChangeType.ToString().ToLowerInvariant();
                    result.Add(new IncludedChange(localItem, changeTypeName));
                }
                return result;
            }
            catch
            {
                await InfoBarHelper.ShowAsync(_package,
                    "Include filter unavailable — analyzing all pending changes.",
                    cancellationToken);
                return null;
            }
        }

        private async Task<DiffResult> BuildIncludedOnlyDiffAsync(
            string tfExe,
            IReadOnlyList<IncludedChange> includedChanges,
            CancellationToken cancellationToken)
        {
            var statusSummary = string.Join(
                Environment.NewLine,
                includedChanges.Select(c => $"{c.ChangeTypeName} {c.LocalItem}"));

            var diffBuilder = new StringBuilder();

            foreach (var change in includedChanges)
            {
                if (diffBuilder.Length >= MaxDiffChars) break;

                var fileDiff = change.IsAdd
                    ? BuildSyntheticAddDiff(change.LocalItem)
                    : await RunTfAsync(tfExe, $"diff /noprompt /format:unified \"{change.LocalItem}\"", cancellationToken);

                if (string.IsNullOrEmpty(fileDiff)) continue;

                diffBuilder.Append(fileDiff);
                if (!fileDiff.EndsWith("\n") && !fileDiff.EndsWith("\r\n"))
                    diffBuilder.AppendLine();
            }

            var diff = diffBuilder.ToString();
            if (diff.Length > MaxDiffChars)
                diff = diff.Substring(0, MaxDiffChars) + "\n[... diff truncated — too large ...]";

            return new DiffResult(
                statusSummary: statusSummary,
                diffContent:   diff.Trim(),
                hasChanges:    true,
                vcsType:       VcsType.Tfvc);
        }

        /// <summary>
        /// Builds a unified-diff block for a newly added file (no server-side base to diff against).
        /// </summary>
        private static string BuildSyntheticAddDiff(string localPath)
        {
            try
            {
                if (!File.Exists(localPath)) return string.Empty;

                var lines = File.ReadAllLines(localPath);
                var sb    = new StringBuilder();
                sb.AppendLine("--- /dev/null");
                sb.AppendLine($"+++ b/{localPath.Replace('\\', '/')}");
                sb.AppendLine($"@@ -0,0 +1,{lines.Length} @@");
                foreach (var line in lines)
                    sb.AppendLine($"+{line}");
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        // ── all-changes fallback path ─────────────────────────────────────────────

        private async Task<DiffResult> BuildAllChangesDiffAsync(
            string tfExe,
            string solutionDir,
            CancellationToken cancellationToken)
        {
            var rawStatus = await RunTfAsync(
                tfExe,
                $"status /recursive /noprompt /format:brief \"{solutionDir}\"",
                cancellationToken);

            var status = FilterStatusLines(rawStatus);
            if (string.IsNullOrWhiteSpace(status))
                return DiffResult.Empty;

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
        }

        // ── helpers ───────────────────────────────────────────────────────────────

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

        // ── types ─────────────────────────────────────────────────────────────────

        private readonly struct IncludedChange
        {
            public IncludedChange(string localItem, string changeTypeName)
            {
                LocalItem      = localItem;
                ChangeTypeName = changeTypeName;
                IsAdd          = changeTypeName.Contains("add");
            }

            public string LocalItem      { get; }
            public string ChangeTypeName { get; }
            public bool   IsAdd          { get; }
        }
    }
}
