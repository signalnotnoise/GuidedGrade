using GuidedGrade.Models;
namespace GuidedGrade.ViewModels;
internal sealed record UnplacedReviewsViewModel(IReadOnlyList<(string Path, SectionFeedback Comment)> Comments,
    Action<string, SectionFeedback> Place);
