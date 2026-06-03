using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace ClaudeCommit.Options
{
    internal sealed class ClaudeCommitOptions : DialogPage
    {
        private const string DefaultGitTemplate =
            "Generate a concise git commit message following the Conventional Commits format " +
            "(type(scope): description).\n\n" +
            "Changed files:\n{status}\n\n" +
            "Diff:\n{diff}\n\n" +
            "Output ONLY the commit message. No explanation, no markdown, no quotes.";

        private const string DefaultTfvcTemplate =
            "Generate a concise TFVC checkin comment following the Conventional Commits format " +
            "(type(scope): description).\n\n" +
            "Pending changes:\n{status}\n\n" +
            "Diff:\n{diff}\n\n" +
            "Output ONLY the checkin comment. No explanation, no markdown, no quotes.";

        // ── Git ───────────────────────────────────────────────────────────────────

        [Category("Generation")]
        [DisplayName("Git Prompt Template")]
        [Description(
            "Template sent to Claude CLI for Git commits. " +
            "Use {diff} for full diff content and {status} for the file status summary.")]
        [Editor(
            typeof(System.ComponentModel.Design.MultilineStringEditor),
            typeof(System.Drawing.Design.UITypeEditor))]
        public string GitPromptTemplate { get; set; } = DefaultGitTemplate;

        // ── TFVC ──────────────────────────────────────────────────────────────────

        [Category("Generation")]
        [DisplayName("TFVC Prompt Template")]
        [Description(
            "Template sent to Claude CLI for TFVC checkin comments. " +
            "Use {diff} for full diff content and {status} for the pending-changes summary.")]
        [Editor(
            typeof(System.ComponentModel.Design.MultilineStringEditor),
            typeof(System.Drawing.Design.UITypeEditor))]
        public string TfvcPromptTemplate { get; set; } = DefaultTfvcTemplate;

        // ── General ───────────────────────────────────────────────────────────────

        [Category("General")]
        [DisplayName("Claude CLI Path")]
        [Description(
            "Absolute path to the claude executable. " +
            "Leave empty to use 'claude' from your system PATH.")]
        public string ClaudeCliPath { get; set; } = string.Empty;
    }
}
