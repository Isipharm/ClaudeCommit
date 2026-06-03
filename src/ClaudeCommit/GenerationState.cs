using System.Threading;

namespace ClaudeCommit
{
    internal sealed class GenerationState
    {
        private readonly object _lock = new object();
        private CancellationTokenSource _cts;

        public bool IsGenerating { get; private set; }

        // Returns (token, success). success==false means generation already active — caller must not proceed.
        public bool TryStart(out CancellationToken token)
        {
            lock (_lock)
            {
                if (IsGenerating)
                {
                    token = CancellationToken.None;
                    return false;
                }
                _cts = new CancellationTokenSource();
                IsGenerating = true;
                token = _cts.Token;
                return true;
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                IsGenerating = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void Cancel()
        {
            lock (_lock)
            {
                _cts?.Cancel();
            }
        }
    }
}
