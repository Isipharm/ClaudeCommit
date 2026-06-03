using System;

namespace ClaudeCommit.Exceptions
{
    internal sealed class ClaudeCliException : Exception
    {
        public ClaudeCliException(string message) : base(message) { }
    }

    internal sealed class ClaudeCliNotFoundException : Exception
    {
        public ClaudeCliNotFoundException()
            : base("Claude CLI not found. Install from https://claude.ai/download or add to PATH.") { }
    }

    internal sealed class NoChangesException : Exception
    {
        public NoChangesException() : base("No changes found in repository.") { }
    }

    internal sealed class NoVcsException : Exception
    {
        public NoVcsException()
            : base("No supported VCS detected. Open a solution inside a Git repo or a TFVC workspace.") { }
    }
}
