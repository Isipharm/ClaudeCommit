using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ClaudeCommit.Commands;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;

namespace ClaudeCommit.UI
{
    internal abstract class ButtonInjectorBase
    {
        private const string MarkerTag = "ClaudeCommit_Injected";
        private readonly ClaudeCommitPackage _package;
        private DispatcherTimer _timer;
        private Button _injectedButton;

        protected ButtonInjectorBase(ClaudeCommitPackage package)
        {
            _package = package;
        }

        protected abstract string[] CandidateIds { get; }

        public void Start()
        {
            _package.GenerationState.IsGeneratingChanged += OnIsGeneratingChanged;

            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(2)
            };
#pragma warning disable VSTHRD010
            _timer.Tick += (s, e) => TryInject();
#pragma warning restore VSTHRD010
            _timer.Start();
        }

        private void OnIsGeneratingChanged(bool isGenerating)
        {
            // May be called from any thread — marshal to UI thread via JTF
#pragma warning disable VSSDK007, VSTHRD110
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                try { UpdateButtonState(isGenerating); }
                catch { }
            });
#pragma warning restore VSSDK007, VSTHRD110
        }

        private void UpdateButtonState(bool isGenerating)
        {
            if (_injectedButton == null) return;

            if (isGenerating)
            {
                _injectedButton.Content = BuildCancelContent();
                _injectedButton.ToolTip = "Cancel Generation";
            }
            else
            {
                _injectedButton.Content = BuildGenerateContent();
                _injectedButton.ToolTip = "Generate Commit Message (Claude AI)";
            }
        }

        private void TryInject()
        {
            try
            {
                if (_injectedButton != null) return;

                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow == null) return;

                FrameworkElement textBox = null;
                foreach (var id in CandidateIds)
                {
                    textBox = FindByAutomationId(mainWindow, id) as FrameworkElement;
                    if (textBox != null) break;
                }
                if (textBox == null) return;

                // Walk up the visual tree to find the Panel that is the EXTERNAL layout
                // container. The AutomationId search may return an element inside the
                // TextBox's own visual template; we skip through TextBox/RichTextBox
                // boundaries so we land on the real hosting panel, not a template panel.
                Panel parent = null;
                FrameworkElement directChild = textBox;

                DependencyObject current = textBox;
                while (true)
                {
                    var up = VisualTreeHelper.GetParent(current);
                    if (up == null) return;

                    // Skip content-host boundaries and wrapper panels:
                    // (a) TextBox / RichTextBox — template internals
                    // (b) any panel whose type name contains "TextBox" (e.g. LabeledTextBox)
                    // (c) panels with ≤ 2 children — these are section-content wrappers
                    //     (e.g. Grid 'contentArea', SectionControl), not layout panels.
                    // Stop at the first panel with ≥ 3 children — that is the true
                    // layout container where the button should live as a sibling.
                    if (up is TextBox || up is RichTextBox
                        || up.GetType().Name.IndexOf("TextBox", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        current = up;
                        continue;
                    }

                    if (up is Panel p)
                    {
                        if (p.Children.Count >= 3)
                        {
                            parent = p;
                            directChild = current as FrameworkElement ?? textBox;
                            break;
                        }
                        // Small wrapper panel — keep walking up
                        current = up;
                        continue;
                    }
                    current = up;
                }

                if (IsAlreadyInjected(parent)) return;

                InjectButton(parent, directChild);
            }
            catch { /* never crash VS */ }
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

        private void InjectButton(Panel parent, FrameworkElement directChild)
        {
            var btn = new Button
            {
                Tag = MarkerTag,
                Content = BuildGenerateContent(),
                ToolTip = "Generate Commit Message (Claude AI)",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(4, 2, 4, 2),
                Padding = new Thickness(6, 4, 6, 4),
            };
            btn.SetResourceReference(FrameworkElement.StyleProperty, VsResourceKeys.ButtonStyleKey);
            btn.SetValue(AutomationProperties.AutomationIdProperty, "claudeCommitButton");
            btn.SetValue(AutomationProperties.NameProperty, "Generate Commit Message");
            btn.Click += OnButtonClick;

            if (parent is Grid grid)
            {
                int insertRow = Grid.GetRow(directChild);
                int col      = Grid.GetColumn(directChild);
                int colSpan  = Grid.GetColumnSpan(directChild);

                grid.RowDefinitions.Insert(insertRow, new RowDefinition { Height = GridLength.Auto });

                foreach (UIElement child in grid.Children)
                {
                    int r = Grid.GetRow(child);
                    if (r >= insertRow)
                        Grid.SetRow(child, r + 1);
                }

                Grid.SetRow(btn, insertRow);
                Grid.SetColumn(btn, col);
                if (colSpan > 1) Grid.SetColumnSpan(btn, colSpan);

                grid.Children.Add(btn);
            }
            else
            {
                int index = parent.Children.IndexOf(directChild);
                if (index < 0) index = parent.Children.Count;
                parent.Children.Insert(index, btn);
            }

            _injectedButton = btn;

            // Sync to current state in case generation started before injection
            UpdateButtonState(_package.GenerationState.IsGenerating);
        }

        private static StackPanel BuildGenerateContent()
        {
            var icon = new CrispImage
            {
                Moniker = KnownMonikers.Comment,
                Width   = 16,
                Height  = 16,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var label = new TextBlock
            {
                Text = "Generate Commit Message",
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            stack.Children.Add(icon);
            stack.Children.Add(label);
            return stack;
        }

        private static StackPanel BuildCancelContent()
        {
            var icon = new CrispImage
            {
                Moniker = KnownMonikers.Cancel,
                Width   = 16,
                Height  = 16,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var label = new TextBlock
            {
                Text = "Cancel Generation",
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            stack.Children.Add(icon);
            stack.Children.Add(label);
            return stack;
        }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            if (_package.GenerationState.IsGenerating)
            {
                _package.GenerationState.Cancel();
                return;
            }

#pragma warning disable VSSDK007, VSTHRD110
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(
                () => GenerateCommitMessageCommand.ExecuteAsync(_package));
#pragma warning restore VSSDK007, VSTHRD110
        }

        private static DependencyObject FindByAutomationId(DependencyObject root, string id)
        {
            if (root == null) return null;

            var stack = new Stack<DependencyObject>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (current is FrameworkElement fe
                    && (fe.Name == id || AutomationProperties.GetAutomationId(fe) == id))
                    return current;

                int count = VisualTreeHelper.GetChildrenCount(current);
                for (int i = count - 1; i >= 0; i--)
                    stack.Push(VisualTreeHelper.GetChild(current, i));
            }

            return null;
        }
    }
}
