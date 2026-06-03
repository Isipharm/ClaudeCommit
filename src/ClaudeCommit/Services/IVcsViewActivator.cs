using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCommit.Services
{
    internal interface IVcsViewActivator
    {
        /// <summary>
        /// Ensures the Git Changes or TFVC Pending Changes panel is visible.
        /// Best-effort: failures are swallowed so injection can still run.
        /// </summary>
        Task EnsureVisibleAsync(VcsType vcsType, CancellationToken cancellationToken);
    }
}
