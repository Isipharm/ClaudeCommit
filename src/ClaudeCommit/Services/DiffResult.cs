namespace ClaudeCommit.Services
{
    internal sealed class DiffResult
    {
        public static readonly DiffResult Empty = new DiffResult(
            statusSummary: string.Empty,
            diffContent:   string.Empty,
            hasChanges:    false,
            vcsType:       VcsType.Unknown);

        public string  StatusSummary { get; }
        public string  DiffContent   { get; }
        public bool    HasChanges    { get; }
        public VcsType VcsType       { get; }

        public DiffResult(string statusSummary, string diffContent, bool hasChanges, VcsType vcsType)
        {
            StatusSummary = statusSummary;
            DiffContent   = diffContent;
            HasChanges    = hasChanges;
            VcsType       = vcsType;
        }
    }
}
