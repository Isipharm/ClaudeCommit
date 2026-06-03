using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ClaudeCommit.Services
{
    internal sealed class GitDiffService : IGitDiffService
    {
        private readonly AsyncPackage _package;

        public GitDiffService(AsyncPackage package) => _package = package;

        public async Task<DiffResult> GetDiffAsync(CancellationToken cancellationToken)
        {
            // GetRepoPathAsync needs the main thread; git I/O must NOT run on it
            var repoPath = await GetRepoPathAsync(cancellationToken);
            if (string.IsNullOrEmpty(repoPath))
                return DiffResult.Empty;

            // Task.Run guarantees thread-pool execution — keeps VS UI thread free during git I/O
            return await Task.Run(async () =>
            {
                // git status --short detects ALL changes: untracked, staged, modified, deleted
                // git diff HEAD fails on repos with no commits — use --cached as fallback
                var statusTask  = RunGitAsync(repoPath, "status --short", cancellationToken);
                var hasHeadTask = RunGitAsync(repoPath, "rev-parse --verify HEAD", cancellationToken);

                var statusOutput = await statusTask;
                var headOutput   = await hasHeadTask;

                var hasHead    = !string.IsNullOrWhiteSpace(headOutput);
                var diffArgs   = hasHead ? "diff HEAD" : "diff --cached";
                var diffOutput = await RunGitAsync(repoPath, diffArgs, cancellationToken);

                return new DiffResult(
                    statusSummary: statusOutput.Trim(),
                    diffContent:   diffOutput.Trim(),
                    hasChanges:    !string.IsNullOrWhiteSpace(statusOutput),
                    vcsType:       VcsType.Git);

            }, cancellationToken);
        }

        private async Task<string> GetRepoPathAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var solution = await _package.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            string solutionDir = null;
            solution?.GetSolutionInfo(out solutionDir, out _, out _);

            if (string.IsNullOrEmpty(solutionDir))
                return null;

            var dir = new DirectoryInfo(solutionDir);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            return null;
        }

        private static async Task<string> RunGitAsync(
            string workingDir,
            string arguments,
            CancellationToken cancellationToken)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName               = "git",
                    Arguments              = arguments,
                    WorkingDirectory       = workingDir,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };

                process.Start();

                // Read stdout and stderr concurrently — not reading stderr causes deadlock
                // when git fills the stderr pipe buffer (e.g. binary warnings, LFS messages)
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask  = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(cancellationToken);

                var output = await outputTask;
                await errorTask;

                return output;
            }
        }
    }
}
