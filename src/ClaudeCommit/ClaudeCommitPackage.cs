using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCommit.Commands;
using ClaudeCommit.Options;
using ClaudeCommit.Services;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace ClaudeCommit
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuids.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(ClaudeCommitOptions), "Claude Commit", "General", 0, 0, true)]
    // Load package on VS startup so commands are registered immediately (no click required)
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class ClaudeCommitPackage : AsyncPackage
    {
        internal IPromptTemplateService PromptTemplateService { get; private set; }
        internal IClaudeCliService      ClaudeCliService      { get; private set; }
        internal IGitDiffService        GitDiffService        { get; private set; }
        internal IVcsDiffService        TfvcDiffService       { get; private set; }
        internal IVcsDetector           VcsDetector           { get; private set; }
        internal IVcsViewActivator      VcsViewActivator      { get; private set; }
        internal ICommitMessageService  CommitMessageService  { get; private set; }
        internal ICommitMessageInjector CommitMessageInjector { get; private set; }
        internal GenerationState        GenerationState       { get; } = new GenerationState();

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Read VS IDE install dir on the main thread so TfExeLocator can run from any thread
            var vsShell = await GetServiceAsync(typeof(SVsShell)) as IVsShell;
            object ideDirObj = null;
            vsShell?.GetProperty((int)__VSSPROPID.VSSPROPID_InstallDirectory, out ideDirObj);
            var ideDir = ideDirObj as string ?? string.Empty;

            var tfExeLocator = new TfExeLocator(ideDir);

            PromptTemplateService = new PromptTemplateService(this);
            GitDiffService        = new GitDiffService(this);
            TfvcDiffService       = new TfvcDiffService(this, tfExeLocator);
            ClaudeCliService      = new ClaudeCliService(() => GetOptions().ClaudeCliPath);
            VcsDetector           = new VcsDetectorService(this, tfExeLocator);
            VcsViewActivator      = new VcsViewActivator(this);
            CommitMessageInjector = new CommitMessageInjector(this);
            CommitMessageService  = new CommitMessageService(
                VcsDetector,
                GitDiffService,
                TfvcDiffService,
                ClaudeCliService,
                PromptTemplateService,
                CommitMessageInjector,
                VcsViewActivator);

            await GenerateCommitMessageCommand.InitializeAsync(this);
            await CancelGenerationCommand.InitializeAsync(this);
        }

        internal ClaudeCommitOptions GetOptions()
            => (ClaudeCommitOptions)GetDialogPage(typeof(ClaudeCommitOptions));
    }
}
