using System.Threading;
using System.Threading.Tasks;
using ClaudeCommit.Exceptions;

namespace ClaudeCommit.Services
{
    internal sealed class CommitMessageService : ICommitMessageService
    {
        private readonly IVcsDetector           _vcsDetector;
        private readonly IGitDiffService        _gitDiffService;
        private readonly IVcsDiffService        _tfvcDiffService;
        private readonly IClaudeCliService      _claudeCliService;
        private readonly IPromptTemplateService _promptTemplateService;
        private readonly ICommitMessageInjector _injector;
        private readonly IVcsViewActivator      _viewActivator;

        public CommitMessageService(
            IVcsDetector           vcsDetector,
            IGitDiffService        gitDiffService,
            IVcsDiffService        tfvcDiffService,
            IClaudeCliService      claudeCliService,
            IPromptTemplateService promptTemplateService,
            ICommitMessageInjector injector,
            IVcsViewActivator      viewActivator)
        {
            _vcsDetector           = vcsDetector;
            _gitDiffService        = gitDiffService;
            _tfvcDiffService       = tfvcDiffService;
            _claudeCliService      = claudeCliService;
            _promptTemplateService = promptTemplateService;
            _injector              = injector;
            _viewActivator         = viewActivator;
        }

        public async Task GenerateAndInjectAsync(CancellationToken cancellationToken)
        {
            var vcsType = await _vcsDetector.DetectAsync(cancellationToken);
            if (vcsType == VcsType.Unknown)
                throw new NoVcsException();

            IVcsDiffService diffService = vcsType == VcsType.Tfvc
                ? _tfvcDiffService
                : _gitDiffService;

            var diff = await diffService.GetDiffAsync(cancellationToken);
            if (!diff.HasChanges)
                throw new NoChangesException();

            var prompt  = _promptTemplateService.BuildPrompt(diff);
            var message = await _claudeCliService.GenerateAsync(prompt, cancellationToken);

            // Ensure the target panel is visible before injecting.
            // Runs concurrently with nothing sensitive — safe to do after generation.
            await _viewActivator.EnsureVisibleAsync(vcsType, cancellationToken);

            await _injector.InjectAsync(message, cancellationToken);
        }
    }
}
