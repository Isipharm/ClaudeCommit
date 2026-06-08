namespace ClaudeCommit.UI
{
    internal sealed class PendingChangesButtonInjector : ButtonInjectorBase
    {
        public PendingChangesButtonInjector(ClaudeCommitPackage package) : base(package) { }

        protected override string[] CandidateIds => new[]
        {
            "Check-in comment",     // VS 2022/2026 TFVC Pending Changes ← confirmed
            "Comment",              // Team Explorer Pending Changes (TFVC older)
            "checkInComment",       // TFVC alt variant
        };
    }
}
