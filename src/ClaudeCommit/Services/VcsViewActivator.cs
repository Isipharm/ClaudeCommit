using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using Microsoft.VisualStudio.Shell;

namespace ClaudeCommit.Services
{
    /// <summary>
    /// Activates the target VCS panel (Git Changes / TFVC Pending Changes) so its
    /// automation tree is rendered before CommitMessageInjector searches for the textbox.
    ///
    /// Two strategies, tried in order:
    ///   1. DTE ExecuteCommand — uses VS built-in command to show the view
    ///   2. Automation tab-click — finds the panel's docking tab and selects/invokes it
    /// Both are best-effort; any failure is swallowed and injection proceeds anyway.
    /// </summary>
    internal sealed class VcsViewActivator : IVcsViewActivator
    {
        private readonly AsyncPackage _package;

        public VcsViewActivator(AsyncPackage package) => _package = package;

        public async Task EnsureVisibleAsync(VcsType vcsType, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            bool activated = TryActivateViaDte(vcsType)
                          || TryActivateViaAutomation(vcsType);

            if (activated)
            {
                // Give VS time to animate the panel and render the WPF content tree
                await Task.Delay(800, cancellationToken);
            }
        }

        // ── Strategy 1: DTE ExecuteCommand ────────────────────────────────────────

        private bool TryActivateViaDte(VcsType vcsType)
        {
            try
            {
#pragma warning disable VSTHRD010 // already on main thread via SwitchToMainThreadAsync above
                // Explicit IServiceProvider cast avoids ambiguous ServiceExtensions overloads (CS0411)
                var dte = ((IServiceProvider)_package).GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                if (dte == null) return false;

                // Ordered by likelihood for each VS version
                var candidates = vcsType == VcsType.Tfvc
                    ? new[]
                    {
                        "View.TFSPendingChanges",          // VS 2022/2026 TFVC panel
                        "View.TeamExplorer",               // fallback — at least shows TE window
                        "Team.NavigateToPendingChangesPage"
                    }
                    : new[]
                    {
                        "Team.Git.GoToGitChanges",         // VS 2022 confirmed
                        "View.GitChanges",                 // possible alias
                        "Git.GoToGitChanges"
                    };

                foreach (var cmd in candidates)
                {
                    try { dte.ExecuteCommand(cmd); return true; }
                    catch { /* command not available in this VS version — try next */ }
                }
#pragma warning restore VSTHRD010
            }
            catch { /* DTE not available */ }

            return false;
        }

        // ── Strategy 2: Automation tab-click ─────────────────────────────────────

        /// <summary>
        /// Walks all top-level devenv windows looking for a tab / button / list-item
        /// whose Name contains the panel label and clicks it.
        /// Handles the common case where the panel is docked but another tab is on top.
        /// </summary>
        private bool TryActivateViaAutomation(VcsType vcsType)
        {
            try
            {
                // Panel title keywords — partial match: "Git Changes" or "Pending Changes" / "Check-in"
                string[] labels = vcsType == VcsType.Tfvc
                    ? new[] { "Pending Changes", "Check-in" }
                    : new[] { "Git Changes" };

                var pid = Process.GetCurrentProcess().Id;

                foreach (var hwnd in NativeMethods.GetProcessTopLevelWindows(pid))
                {
                    AutomationElement root;
                    try { root = AutomationElement.FromHandle(hwnd); }
                    catch { continue; }
                    if (root == null) continue;

                    foreach (var label in labels)
                    {
                        if (TryInvokeTab(root, label)) return true;
                    }
                }
            }
            catch { /* automation errors are non-fatal */ }

            return false;
        }

        private static bool TryInvokeTab(AutomationElement root, string labelFragment)
        {
            // VS docking tabs can be TabItem, ListItem, or Button depending on the VS version
            // Use a name-contains approach via TreeWalker to avoid missing partial matches
            var nameCondition = new PropertyCondition(
                AutomationElement.NameProperty, labelFragment, PropertyConditionFlags.IgnoreCase);

            foreach (var ctrlType in new[] { ControlType.TabItem, ControlType.ListItem, ControlType.Button })
            {
                var condition = new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ctrlType),
                    nameCondition);

                AutomationElement tab;
                try { tab = root.FindFirst(TreeScope.Descendants, condition); }
                catch { continue; }
                if (tab == null) continue;

                // Try SelectionItemPattern (tab selection)
                if (tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object siObj)
                    && siObj is SelectionItemPattern si)
                {
                    si.Select();
                    return true;
                }

                // Try InvokePattern (button-style tab)
                if (tab.TryGetCurrentPattern(InvokePattern.Pattern, out object invObj)
                    && invObj is InvokePattern inv)
                {
                    inv.Invoke();
                    return true;
                }
            }

            return false;
        }
    }
}
