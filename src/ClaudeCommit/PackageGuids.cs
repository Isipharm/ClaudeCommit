using System;

namespace ClaudeCommit
{
    internal static class PackageGuids
    {
        public const string PackageGuidString    = "2E4A8F3C-1B5D-4E6A-9C7B-0D3E5F7A9B1C";
        public const string CommandSetGuidString = "3F5B9E4D-2C6A-4F7B-8D9E-1E4F6A8B0C2D";

        public static readonly Guid PackageGuid    = new Guid(PackageGuidString);
        public static readonly Guid CommandSetGuid = new Guid(CommandSetGuidString);
    }

    internal static class PackageIds
    {
        public const int GenerateCommitMessageCommandId = 0x0100;
        public const int CancelGenerationCommandId      = 0x0101;
        public const int ClaudeCommitToolsGroup          = 0x1020;
        public const int ClaudeCommitExtensionsGroup     = 0x1021;
    }
}
