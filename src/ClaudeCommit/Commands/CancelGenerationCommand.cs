using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace ClaudeCommit.Commands
{
    internal sealed class CancelGenerationCommand
    {
        public static async Task InitializeAsync(ClaudeCommitPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null) return;

            var cmdId   = new CommandID(PackageGuids.CommandSetGuid, PackageIds.CancelGenerationCommandId);
            var command = new OleMenuCommand((s, e) => package.GenerationState.Cancel(), cmdId);

            command.BeforeQueryStatus += (s, e) =>
            {
                command.Visible = package.GenerationState.IsGenerating;
                command.Enabled = package.GenerationState.IsGenerating;
            };

            commandService.AddCommand(command);
        }
    }
}
