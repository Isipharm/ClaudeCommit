using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCommit.Services
{
    internal interface IVcsDetector
    {
        Task<VcsType> DetectAsync(CancellationToken cancellationToken);
    }
}
