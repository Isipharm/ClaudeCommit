using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCommit.Services
{
    // Process.WaitForExitAsync doesn't exist in .NET 4.7.2 — it was added in .NET 5
    internal static class ProcessExtensions
    {
        public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>();

            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => tcs.TrySetResult(true);

            if (process.HasExited)
            {
                tcs.TrySetResult(true);
                return tcs.Task;
            }

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    tcs.TrySetCanceled(cancellationToken);
                    try { process.Kill(); }
                    catch { /* process may have already exited */ }
                });
            }

            return tcs.Task;
        }
    }
}
