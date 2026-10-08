using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class AssignmentDetailsViewModel
{
    internal State<string> Course { get; } = new("General");
    internal State<string> Title { get; } = new("");
    internal State<string> ReviewFiles { get; } = new("");
    internal State<string> LogPath { get; } = new("");
    internal State<string> Requirements { get; } = new("");
}
