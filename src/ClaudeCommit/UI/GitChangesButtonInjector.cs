namespace ClaudeCommit.UI
{
    internal sealed class GitChangesButtonInjector : ButtonInjectorBase
    {
        public GitChangesButtonInjector(ClaudeCommitPackage package) : base(package) { }

        protected override string[] CandidateIds => new[]
        {
            "Commit comment",       // VS 2022 Git Changes panel       ← confirmed
            "commentTextBox",       // VS 2019 TeamExplorer Git (legacy)
            "textCommitMessage",    // possible variant
            "CommitMessageTextBox", // possible variant
        };
    }
}
