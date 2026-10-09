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
        private bool IsSelectedFile(string path)
        {
            var selected = _selectedTabButton?.Tag as string;
            return !string.IsNullOrWhiteSpace(selected) && string.Equals(selected, path, StringComparison.OrdinalIgnoreCase);
        }

        private void SelectTab(FileTab btn)
        {
            _selectedTabButton = btn;
            _activeFile.Value = btn.Tag;

            var path = (string)btn.Tag!;
            codeEditor.Text = BoundedTextReader.Read(path);
            RefreshReviewSelection();
            SetEmptyState(false);
            UpdateViolationsStatus();
        }

        private void LoadCommentsForFile(string filePath)
        {
            // File/assignment selection reads storage rather than reusing a stale cache.
            RefreshPersistedComments(filePath);
            var comments = _fileComments[filePath];

            RenderCommentsForFile(filePath);
            RestoreApprovedFeedback(filePath, comments);
        }

        private void RenderCommentsForFile(string filePath)
        {
            // Async results may finish after this tab has been closed or replaced.
            if (!IsSelectedFile(filePath)) return;
            _commentLayer?.ClearComments();

            if (!_fileComments.TryGetValue(filePath, out var comments))
                return;

            foreach (var comment in comments.Where(c => (MatchesCurrentReview(c) || c.ReviewContext.Length == 0) && c.ReviewStatus != Models.FeedbackReviewStatus.Rejected))
            {
                _commentLayer?.AddComment(comment, comment.StartLine, comment.EndLine, earlierReview: _currentAssignment != null && comment.ReviewContext.Length == 0);
            }
        }

        private void ClearFileTabs()
        {
            foreach (var tab in _fileTabs)
            {
                if (tab.Tag is string tabPath)
                {
                    PersistCommentsForFile(tabPath);
                }
            }

            _fileTabs.Clear();
            _activeFile.Value = "";
            _selectedTabButton = null;
            codeEditor.Text = string.Empty;
            _violationHighlighter.Clear();
            _gradingView.Clear();
            RefreshGradeSelection();
            SetEmptyState(true);
        }

        private void SetEmptyState(bool isEmpty)
        {
            if (isEmpty) _commentLayer?.ClearComments();
            emptyStateOverlay.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PopulateTreeView(string rootPath)
        {
            fileTreeView.Items.Clear();

            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                Debug.WriteLine($"Invalid root path: {rootPath}");
                return;
            }

            try
            {
                var rootItem = new Models.FileSystemItem(rootPath, true)
                {
                    IsExpanded = true
                };

                LoadDirectory(rootItem, rootPath);
                fileTreeView.Items.Add(rootItem);

                Debug.WriteLine($"Tree populated with root: {rootPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error populating tree: {ex.Message}");
                MessageBox.Show($"Error loading directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDirectory(Models.FileSystemItem parentItem, string directoryPath)
        {
            var warnings = SubmissionFolderLoader.Populate(parentItem, directoryPath);
            if (warnings.Count > 0)
                MessageBox.Show(string.Join("\n", warnings), "Submission folders", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is not Models.FileSystemItem selectedItem)
                return;

            Debug.WriteLine($"Tree item selected: {selectedItem.FullPath} (IsDirectory: {selectedItem.IsDirectory})");

            // Only open files, not directories
            // Directories just expand/collapse in the tree
            // Solution files are launched from the right-click menu instead of the editor.
            if (!selectedItem.IsDirectory && !selectedItem.IsSolution)
            {
                // Recycling can restore selection without changing the active file.
                // Reopening it would reset the retained editor's document and caret.
                if (_selectedTabButton?.Tag is string activePath &&
                    string.Equals(activePath, selectedItem.FullPath, StringComparison.OrdinalIgnoreCase)) return;
                OpenFileInTab(selectedItem.FullPath);
            }
        }

        private void OpenFileInTab(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"File not found: {filePath}");
                return;
            }

            try
            {
                // Check if file is already open
                foreach (var tab in _fileTabs)
                {
                    if (tab.Tag is string existingPath && existingPath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectTab(tab);
                        Debug.WriteLine($"File already open, switching to tab: {filePath}");
                        return;
                    }
                }

                var isFirstTab = _fileTabs.Count == 0;
                var tabButton = CreateFileTab(filePath, isFirstTab);
                _fileTabs.Add(tabButton);
                SelectTab(tabButton);

                if (_fileTabs.Count == 1)
                {
                    SetEmptyState(false);
                }

                Debug.WriteLine($"Opened file in new tab: {filePath}");

                // Update violations for this file
                UpdateViolationsStatus();

                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening file {filePath}: {ex.Message}");
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenTreeItemInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (fileTreeView.SelectedItem is not Models.FileSystemItem selectedItem)
                return;

            try
            {
                var arguments = selectedItem.IsDirectory
                    ? $"\"{selectedItem.FullPath}\""
                    : $"/select,\"{selectedItem.FullPath}\"";
                Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening in explorer: {ex.Message}");
                MessageBox.Show($"Error opening in File Explorer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<Models.FileSystemItem> GetCheckedFiles(System.Collections.IEnumerable items)
        {
            var result = new List<Models.FileSystemItem>();

            foreach (var item in items)
            {
                if (item is Models.FileSystemItem fsItem)
                {
                    // Add if it's a checked file
                    if (!fsItem.IsDirectory && fsItem.IsCheckedForAnalysis)
                    {
                        result.Add(fsItem);
                    }
                    if (fsItem.Children != null)
                    {
                        result.AddRange(GetCheckedFiles(fsItem.Children));
                    }
                }
            }

            return result;
        }

        private static FileTab CreateFileTab(string filePath, bool first = false) => new(filePath);

        private void CloseFileTab(string filePath)
        {
            PersistCommentsForFile(filePath);

            var matchingTab = _fileTabs
                .FirstOrDefault(tab => tab.Tag is string existingPath && existingPath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            if (matchingTab != null)
            {
                _fileTabs.Remove(matchingTab);
            }

            if (_selectedTabButton != null && _selectedTabButton.Tag is string selectedPath &&
                string.Equals(selectedPath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                _selectedTabButton = null;
                if (_fileTabs.Count > 0)
                {
                    var nextTab = _fileTabs[0];
                    SelectTab(nextTab);
                }
                else
                {
                    codeEditor.Text = string.Empty;
                    _violationHighlighter.Clear();
                    SetEmptyState(true);
                }
            }

            if (_fileTabs.Count == 0)
            {
                _selectedTabButton = null;
                _activeFile.Value = "";
                codeEditor.Text = string.Empty;
                _violationHighlighter.Clear();
                SetEmptyState(true);
            }
            RefreshGradeSelection();
        }

        // --- Status Bar -------------------------------------------------------

    }
}
