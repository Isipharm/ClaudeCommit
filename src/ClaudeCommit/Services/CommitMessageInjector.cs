using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using ClaudeCommit.UI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ClaudeCommit.Services
{
    internal sealed class CommitMessageInjector : ICommitMessageInjector
    {
        private readonly AsyncPackage _package;

        public CommitMessageInjector(AsyncPackage package) => _package = package;

        public async Task InjectAsync(string message, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (!await TryInjectViaAutomationAsync(message))
            {
                // Automation failed — dump diagnostic info to Output window, then fall back to clipboard
                await WriteDiagnosticsAsync(cancellationToken);

                Clipboard.SetText(message);
                await InfoBarHelper.ShowAsync(
                    _package,
                    "Commit message copied to clipboard — paste into the Git Changes commit box (Ctrl+V).",
                    cancellationToken);
            }
        }

        private async Task<bool> TryInjectViaAutomationAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var commitTextBox = await FindCommitTextBoxAsync();
                if (commitTextBox == null) return false;

                // Primary: ValuePattern.SetValue — fires WPF dependency-property changed → MVVM binding update
                if (commitTextBox.TryGetCurrentPattern(ValuePattern.Pattern, out object vpObj)
                    && vpObj is ValuePattern vp
                    && !vp.Current.IsReadOnly)
                {
                    vp.SetValue(message);
                    return true;
                }

                // Secondary: focus + clipboard + keybd_event (VS 2022 WPF TextBox fallback)
                return TryInjectViaKeyboard(commitTextBox, message);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInjectViaKeyboard(AutomationElement element, string message)
        {
            try
            {
                var hwnd = GetParentHwnd(element);
                if (hwnd != IntPtr.Zero)
                    NativeMethods.SetForegroundWindow(hwnd);

                element.SetFocus();
                Thread.Sleep(150);

                Clipboard.SetText(message);
                NativeMethods.SendCtrlA();
                Thread.Sleep(30);
                NativeMethods.SendCtrlV();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IntPtr GetParentHwnd(AutomationElement element)
        {
            var walker  = TreeWalker.ControlViewWalker;
            var current = element;
            while (current != null)
            {
                try
                {
                    var hwnd = new IntPtr(current.Current.NativeWindowHandle);
                    if (hwnd != IntPtr.Zero) return hwnd;
                }
                catch { /* element removed from tree */ }
                current = walker.GetParent(current);
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Searches for the Git Changes commit-message text box.
        /// Tries multiple known automation IDs across VS versions:
        ///   "commentTextBox"      — VS 2019 TeamExplorer Git
        ///   "textCommitMessage"   — VS 2022 new Git Changes panel
        ///   "CommitMessageTextBox"— possible alternate
        /// Searches all top-level windows of devenv so floating panels are found too.
        /// </summary>
        private async Task<AutomationElement> FindCommitTextBoxAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            string[] candidateIds =
            {
                "Commit comment",       // VS 2022 Git Changes panel       ← confirmed
                "Check-in comment",     // VS 2026 TFVC Pending Changes    ← confirmed
                "Comment",              // Team Explorer Pending Changes (TFVC older)
                "checkInComment",       // TFVC alt variant
                "commentTextBox",       // VS 2019 TeamExplorer Git (legacy)
                "textCommitMessage",    // possible variant
                "CommitMessageTextBox", // possible variant
            };

            // Search each top-level window owned by devenv
            foreach (var hwnd in NativeMethods.GetProcessTopLevelWindows(Process.GetCurrentProcess().Id))
            {
                AutomationElement root;
                try { root = AutomationElement.FromHandle(hwnd); }
                catch { continue; }
                if (root == null) continue;

                foreach (var id in candidateIds)
                {
                    AutomationElement el;
                    try
                    {
                        el = root.FindFirst(TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.AutomationIdProperty, id));
                    }
                    catch { continue; }

                    if (el != null) return el;
                }
            }

            return null;
        }

        // ── diagnostics ──────────────────────────────────────────────────────────

        /// <summary>
        /// Writes all automation IDs of Edit controls found in devenv windows to the VS Output pane.
        /// Lets us identify the correct commit-message textbox ID without guessing.
        /// </summary>
        private async Task WriteDiagnosticsAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                var outputWindow = await _package.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outputWindow == null) return;

                var paneId = new Guid("7B7B8B8C-1B5D-4E6A-9C7B-0D3E5F7A9B1C");
                outputWindow.CreatePane(ref paneId, "ClaudeCommit", 1, 1);
                outputWindow.GetPane(ref paneId, out var pane);
                if (pane == null) return;

                pane.OutputString($"[ClaudeCommit] Commit textbox not found. Discovered Edit controls:\r\n");

                foreach (var hwnd in NativeMethods.GetProcessTopLevelWindows(Process.GetCurrentProcess().Id))
                {
                    AutomationElement root;
                    try { root = AutomationElement.FromHandle(hwnd); }
                    catch { continue; }
                    if (root == null) continue;

                    AutomationElementCollection edits;
                    try
                    {
                        edits = root.FindAll(TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                    }
                    catch { continue; }

                    foreach (AutomationElement edit in edits)
                    {
                        try
                        {
                            var name = edit.Current.Name;
                            var id   = edit.Current.AutomationId;
                            pane.OutputString($"  AutomationId=\"{id}\"  Name=\"{name}\"\r\n");
                        }
                        catch { /* element gone */ }
                    }
                }

                pane.Activate();
            }
            catch { /* diagnostics must never crash the main path */ }
        }

        // GetProcessTopLevelWindows lives in NativeMethods (shared with VcsViewActivator)
    }
}
