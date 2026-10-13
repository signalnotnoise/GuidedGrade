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
        private void ClearReviewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (fileTreeView.SelectedItem is not Models.FileSystemItem item || item.IsDirectory)
                return;
            try
            {
                // Delete first: a storage failure must not leave a falsely cleared UI.
                _commentPersistenceService.DeleteComments(item.FullPath);
                _reviewGeneration.Clear(item.FullPath);
                RefreshPersistedComments(item.FullPath);
                if (IsSelectedFile(item.FullPath))
                    _commentLayer?.ClearComments();
                Title = $"GuidedGrade - Review cleared for {item.Name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not clear the review: {ex.Message}", "Clear review", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshTreeView_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_openedDirectoryPath))
            {
                PopulateTreeView(_openedDirectoryPath);
            }
        }

        private void FileTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = FindVisualParent<System.Windows.Controls.TreeViewItem>(e.OriginalSource as DependencyObject);
            if (treeViewItem == null)
                return;

            treeViewItem.IsSelected = true;
            treeViewItem.Focus();
        }

        private void FileTreeContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu)
                return;

            var isSolution = fileTreeView.SelectedItem is Models.FileSystemItem item && item.IsSolution;
            var visibility = isSolution ? Visibility.Visible : Visibility.Collapsed;

            foreach (var obj in menu.Items)
            {
                if (obj is FrameworkElement element && Equals(element.Tag, "solution-command"))
                    element.Visibility = visibility;
                if (obj is FrameworkElement fileCommand && Equals(fileCommand.Tag, "file-command"))
                    fileCommand.Visibility = fileTreeView.SelectedItem is Models.FileSystemItem selected && !selected.IsDirectory
                        ? Visibility.Visible : Visibility.Collapsed;
            }
        }

    }
}
