namespace ClaudeCommit.Services
{
    // Marker interface for the Git-specific diff provider.
    // Git diff capability is now expressed through IVcsDiffService;
    // this interface exists for type-safe injection when Git-specific
    // behaviour needs to be added in the future.
    internal interface IGitDiffService : IVcsDiffService { }
}
