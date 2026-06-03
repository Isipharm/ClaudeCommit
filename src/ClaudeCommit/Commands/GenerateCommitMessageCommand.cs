using System;
using System.ComponentModel.Design;
using System.Threading;
using ClaudeCommit.Exceptions;
using ClaudeCommit.Services;
using ClaudeCommit.UI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace ClaudeCommit.Commands
{
    internal sealed class GenerateCommitMessageCommand
    {
        public static async Task InitializeAsync(ClaudeCommitPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null) return;

            var cmdId   = new CommandID(PackageGuids.CommandSetGuid, PackageIds.GenerateCommitMessageCommandId);
            var command = new OleMenuCommand(
                (s, e) =>
                {
#pragma warning disable VSSDK007, VSTHRD110
                    _ = ThreadHelper.JoinableTaskFactory.RunAsync(() => ExecuteAsync(package));
#pragma warning restore VSSDK007, VSTHRD110
                },
                cmdId);

            command.BeforeQueryStatus += (s, e) =>
            {
                bool generating = package.GenerationState.IsGenerating;
                command.Enabled = !generating;
                command.Text    = generating
                    ? "Claude: Generating commit message..."
                    : "Generate Commit Message with Claude";
            };

            commandService.AddCommand(command);
        }

        internal static async Task ExecuteAsync(ClaudeCommitPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!package.ClaudeCliService.IsCliAvailable())
            {
                await InfoBarHelper.ShowErrorAsync(
                    package,
                    "Claude CLI not found. Install from https://claude.ai/download or set path in Tools > Options > Claude Commit.",
                    CancellationToken.None);
                return;
            }

            // Atomic test-and-set — prevents TOCTOU race from rapid double-click
            if (!package.GenerationState.TryStart(out var cancellationToken))
                return;

            var statusBar = await package.GetServiceAsync(typeof(SVsStatusbar)) as IVsStatusbar;
            statusBar?.SetText("Claude: Generating commit message...");

            try
            {
                await package.CommitMessageService.GenerateAndInjectAsync(cancellationToken);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                statusBar?.SetText("Claude: Commit message generated.");
            }
            catch (OperationCanceledException)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                statusBar?.SetText("Claude: Generation cancelled.");
            }
            catch (NoChangesException)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                statusBar?.SetText("Claude: No changes to commit.");
                await InfoBarHelper.ShowAsync(package, "No changes to commit.", CancellationToken.None);
            }
            catch (NoVcsException ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                statusBar?.SetText("Claude: No VCS detected.");
                await InfoBarHelper.ShowErrorAsync(package, ex.Message, CancellationToken.None);
            }
            catch (ClaudeCliException ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                statusBar?.SetText($"Claude error: {ex.Message}");
                await InfoBarHelper.ShowErrorAsync(package, $"Claude CLI error: {ex.Message}", CancellationToken.None);
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                statusBar?.SetText($"Claude error: {ex.Message}");
                await InfoBarHelper.ShowErrorAsync(package, $"ClaudeCommit error: {ex.Message}", CancellationToken.None);
            }
            finally
            {
                package.GenerationState.Stop();
            }
        }
    }
}
