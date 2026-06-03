using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCommit.Services
{
    internal interface ICommitMessageInjector
    {
        Task InjectAsync(string message, CancellationToken cancellationToken);
    }
}
