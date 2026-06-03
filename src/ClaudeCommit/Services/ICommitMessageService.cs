using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCommit.Services
{
    internal interface ICommitMessageService
    {
        Task GenerateAndInjectAsync(CancellationToken cancellationToken);
    }
}
