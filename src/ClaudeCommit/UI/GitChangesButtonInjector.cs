using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ClaudeCommit.Commands;
using Microsoft.VisualStudio.Shell;

namespace ClaudeCommit.UI
{
    internal sealed class GitChangesButtonInjector
    {
        private const string MarkerTag = "ClaudeCommit_Injected";
        private readonly ClaudeCommitPackage _package;
        private DispatcherTimer _timer;

        public GitChangesButtonInjector(ClaudeCommitPackage package)
        {
            _package = package;
        }

        public void Start()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _timer.Tick += (s, e) => TryInject();
            _timer.Start();
        }

        private void TryInject()
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null) return;

            var fetchButton = FindByAutomationId(mainWindow, "fetchButton") as FrameworkElement;
            if (fetchButton == null) return;

            var parent = VisualTreeHelper.GetParent(fetchButton) as Panel;
            if (parent == null) return;

            if (IsAlreadyInjected(parent)) return;

            InjectButton(parent, fetchButton);
        }

        private static bool IsAlreadyInjected(Panel parent)
        {
            foreach (UIElement child in parent.Children)
            {
                if ((child as FrameworkElement)?.Tag as string == MarkerTag)
                    return true;
            }
            return false;
        }

        private void InjectButton(Panel parent, FrameworkElement reference)
        {
            var sep = new Separator
            {
                Tag = MarkerTag,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(2, 0, 2, 0),
            };
            sep.SetValue(AutomationProperties.AutomationIdProperty, "claudeCommitSeparator");

            var refCtrl = reference as System.Windows.Controls.Control;
            var btn = new Button
            {
                Tag = MarkerTag,
                Content = "✨",
                ToolTip = "Generate Commit Message (Claude AI)",
                Style = reference.Style,
                Margin = reference.Margin,
                Padding = refCtrl?.Padding ?? new Thickness(4, 2, 4, 2),
            };
            btn.SetValue(AutomationProperties.AutomationIdProperty, "claudeCommitButton");
            btn.SetValue(AutomationProperties.NameProperty, "Generate Commit Message");
            btn.Click += OnGenerateClick;

            parent.Children.Add(sep);
            parent.Children.Add(btn);
        }

        private void OnGenerateClick(object sender, RoutedEventArgs e)
        {
#pragma warning disable VSSDK007, VSTHRD110
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(
                () => GenerateCommitMessageCommand.ExecuteAsync(_package));
#pragma warning restore VSSDK007, VSTHRD110
        }

        private static DependencyObject FindByAutomationId(DependencyObject root, string id)
        {
            if (root == null) return null;

            if (root is FrameworkElement fe
                && (fe.Name == id || AutomationProperties.GetAutomationId(fe) == id))
                return root;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var result = FindByAutomationId(VisualTreeHelper.GetChild(root, i), id);
                if (result != null) return result;
            }
            return null;
        }
    }
}
