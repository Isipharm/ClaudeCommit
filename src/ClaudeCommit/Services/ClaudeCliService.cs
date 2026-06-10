using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCommit.Exceptions;

namespace ClaudeCommit.Services
{
    internal sealed class ClaudeCliService : IClaudeCliService
    {
        private readonly Func<string> _getCliPath;
        private readonly Func<string> _getModel;

        // getCliPath: returns user-configured CLI path from options; falls back to "claude" on PATH
        // getModel: returns user-configured model alias/ID; null or empty omits --model flag
        public ClaudeCliService(Func<string> getCliPath = null, Func<string> getModel = null)
        {
            _getCliPath = getCliPath ?? (() => null);
            _getModel   = getModel   ?? (() => null);
        }

        private string CliPath
        {
            get
            {
                var configured = _getCliPath();
                return string.IsNullOrWhiteSpace(configured) ? "claude" : configured;
            }
        }

        public bool IsCliAvailable()
        {
            try
            {
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName               = CliPath,
                    Arguments              = "--version",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                }))
                {
                    process?.WaitForExit(3000);
                    return process?.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GenerateAsync(string fullPrompt, CancellationToken cancellationToken)
        {
            using (var process = new Process())
            {
                var model = _getModel?.Invoke();
                var args  = string.IsNullOrWhiteSpace(model) ? "--print" : $"--model {model} --print";

                process.StartInfo = new ProcessStartInfo
                {
                    FileName               = CliPath,
                    Arguments              = args,
                    UseShellExecute        = false,
                    RedirectStandardInput  = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };

                process.Start();

                // Write full prompt (template + diff) to stdin; claude --print reads it as the prompt
                await process.StandardInput.WriteAsync(fullPrompt);
                process.StandardInput.Close();

                // Start reads before waiting — both pipes must drain concurrently to avoid deadlock
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask  = process.StandardError.ReadToEndAsync();

                string output = null, error = null;
                try
                {
                    await process.WaitForExitAsync(cancellationToken);
                }
                finally
                {
                    // Always drain pipes before the using-block disposes the process.
                    // On cancellation the process is already killed; reads return quickly.
                    output = await outputTask;
                    error  = await errorTask;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (process.ExitCode != 0)
                    throw new ClaudeCliException($"Claude CLI exited {process.ExitCode}: {error.Trim()}");

                return output.Trim();
            }
        }
    }
}
