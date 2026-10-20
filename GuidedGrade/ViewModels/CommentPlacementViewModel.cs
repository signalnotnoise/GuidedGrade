using System.Windows;
namespace GuidedGrade.ViewModels;
internal sealed record CommentPlacementViewModel(bool Pinned, bool Enabled, Action<DependencyObject> Drag, Action TogglePin)
{
    internal string Hint => Pinned ? "Unpin this comment to move it to another line." : "Drag this handle onto a code line, then pin the comment.";
}
