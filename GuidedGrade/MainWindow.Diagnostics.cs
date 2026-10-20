using GuidedGrade.ViewModels;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using GuidedGrade.Models;
using GuidedGrade.Services;
using GuidedGrade.Views;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;


namespace GuidedGrade
{
    public partial class MainWindow
    {
        private void UpdateViolationsStatus()
        {
            Debug.WriteLine("=== UpdateViolationsStatus() CALLED ===");

            // Load violations from config file or use defaults
            var configService = new ViolationsConfigService();
            var violationTerms = configService.LoadViolations(_openedDirectoryPath);

            Debug.WriteLine($"Opened Directory Path: {_openedDirectoryPath ?? "(null)"}");
            Debug.WriteLine($"Loaded {violationTerms.Count} violation terms: {string.Join(", ", violationTerms)}");
            Debug.WriteLine($"File patterns: {string.Join(", ", configService.FilePatterns)}");

            var matcher = new ViolationsMatcher(violationTerms);

            // Collect text from all loaded tabs, not just the visible one
            var allViolations = new List<Violation>();
            Debug.WriteLine($"Scanning {_fileTabs.Count} file tabs...");

            foreach (var tab in _fileTabs)
            {
                var path = (string)tab.Tag!;
                Debug.WriteLine($"  Checking file: {path}");

                if (!File.Exists(path))
                {
                    Debug.WriteLine($"    SKIPPED: File does not exist");
                    continue;
                }

                // Only scan files that match the configured patterns
                if (!configService.ShouldScanFile(path))
                {
                    Debug.WriteLine($"    SKIPPED: File does not match configured patterns");
                    continue;
                }

                Debug.WriteLine($"    SCANNING: File matches pattern");

                var text = BoundedTextReader.Read(path);
                Debug.WriteLine($"    File length: {text.Length} characters");

                var violations = matcher.GetViolations(text);
                Debug.WriteLine($"    Found {violations.Count} violations in this file");

                // Tag each violation with its source file for the tooltip
                foreach (var v in violations)
                {
                    Debug.WriteLine($"      - Line {v.LineNumber}: '{v.MatchedText}' {v.Context}");
                    allViolations.Add(new Violation(
                        v.LineNumber,
                        v.MatchedText,
                        v.Context,
                        $"{Path.GetFileName(path)} — {v.LineContent}"));
                }
            }

            Debug.WriteLine($"TOTAL VIOLATIONS FOUND: {allViolations.Count}");

            // Highlight violations only in the currently visible file
            var visibleViolations = allViolations
                .Where(v => v.LineContent.Contains(
                    _selectedTabButton != null
                        ? Path.GetFileName((string)_selectedTabButton.Tag!)
                        : string.Empty))
                .ToList();

            Debug.WriteLine($"Visible violations (for current tab): {visibleViolations.Count}");

            // Actually re-run for visible file to get correct line numbers
            if (_selectedTabButton != null)
            {
                var visiblePath = (string)_selectedTabButton.Tag!;
                Debug.WriteLine($"Selected tab: {visiblePath}");

                // Only highlight if the visible file should be scanned
                if (configService.ShouldScanFile(visiblePath))
                {
                    var visibleText = BoundedTextReader.Read(visiblePath);
                    var visibleFileViolations = matcher.GetViolations(visibleText);
                    var lineNumbers = visibleFileViolations.Select(v => v.LineNumber).ToList();
                    Debug.WriteLine($"Highlighting lines: {string.Join(", ", lineNumbers)}");
                    _violationHighlighter.SetViolationLines(lineNumbers);
                }
                else
                {
                    Debug.WriteLine("Visible file does not match scan patterns - clearing highlights");
                    _violationHighlighter.Clear();
                }
            }
            else
            {
                Debug.WriteLine("No selected tab - clearing highlights");
                _violationHighlighter.Clear();
            }

            _violationCount.Value = allViolations.Count.ToString();
            Debug.WriteLine($"Status bar updated to: {allViolations.Count}");

            if (allViolations.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Found {allViolations.Count} violation(s):\n");

                // Group by file for readability
                var byFile = allViolations
                    .GroupBy(v => v.LineContent.Split('—')[0].Trim());

                foreach (var group in byFile)
                {
                    sb.AppendLine($"-- {group.Key}");
                    foreach (var v in group)
                        sb.AppendLine($"  Line {v.LineNumber}: '{v.MatchedText}'  {v.Context}");
                    sb.AppendLine();
                }

                _violationTooltip.Content = new WpfTextBlock
                    {
                        Text = sb.ToString().TrimEnd(),
                        FontFamily = new FontFamily("Consolas"),
                        MaxWidth = 600,
                        TextWrapping = TextWrapping.Wrap
                };
            }
            else
            {
                _violationTooltip.Content = null;
            }

            _violationColor.Value = allViolations.Count switch
            {
                > 3 => "#FF6666",
                > 0 => "#FFA500",
                _ => "#CCCCCC"
            };

            // Update violations list panel
            violationsList.ItemsSource = allViolations;
            Debug.WriteLine($"Violations list updated with {allViolations.Count} items");
            Debug.WriteLine($"Violations panel visibility: {violationsPanel.Visibility}");
            Debug.WriteLine("=== UpdateViolationsStatus() COMPLETE ===\n");
        }

        private void StatusViolations_Click(object sender, MouseButtonEventArgs e)
            => SelectToolsPanel(true, toggleSelected: true);

        private void OpenRuntimeTerminalPanel() => SelectToolsPanel(false);

        private void AppendToRuntimeTerminal(string text, Brush? brush = null)
            => _runtimeTerminal.Append(text, brush ?? Brushes.LightGray);

        private void ViolationsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (violationsList.SelectedItem is Violation violation)
            {
                // Navigate to the line in the editor
                try
                {
                    var line = codeEditor.Document.GetLineByNumber(violation.LineNumber);
                    codeEditor.ScrollToLine(violation.LineNumber);
                    codeEditor.Select(line.Offset, line.Length);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error navigating to line: {ex.Message}");
                }
            }
        }

        // TODO: Remove?
    }
}
