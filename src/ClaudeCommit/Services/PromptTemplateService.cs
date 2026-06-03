using ClaudeCommit.Options;
using Microsoft.VisualStudio.Shell;

namespace ClaudeCommit.Services
{
    internal sealed class PromptTemplateService : IPromptTemplateService
    {
        private readonly AsyncPackage _package;

        public PromptTemplateService(AsyncPackage package) => _package = package;

        public string GitTemplate
        {
            get => GetOptions().GitPromptTemplate;
            set => GetOptions().GitPromptTemplate = value;
        }

        public string TfvcTemplate
        {
            get => GetOptions().TfvcPromptTemplate;
            set => GetOptions().TfvcPromptTemplate = value;
        }

        public string BuildPrompt(DiffResult diff)
        {
            var template = diff.VcsType == VcsType.Tfvc ? TfvcTemplate : GitTemplate;
            return template
                .Replace("{status}", diff.StatusSummary)
                .Replace("{diff}",   diff.DiffContent);
        }

        private ClaudeCommitOptions GetOptions()
            => (ClaudeCommitOptions)_package.GetDialogPage(typeof(ClaudeCommitOptions));
    }
}
