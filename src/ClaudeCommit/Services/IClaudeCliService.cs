using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCommit.Services
{
    internal interface IClaudeCliService
    {
        bool IsCliAvailable();
        Task<string> GenerateAsync(string fullPrompt, CancellationToken cancellationToken);
    }
}
