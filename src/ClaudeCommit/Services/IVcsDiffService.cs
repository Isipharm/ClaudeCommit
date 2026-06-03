using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCommit.Services
{
    internal interface IVcsDiffService
    {
        Task<DiffResult> GetDiffAsync(CancellationToken cancellationToken);
    }
}
