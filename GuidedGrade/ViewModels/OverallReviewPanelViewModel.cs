using GuidedGrade.Models;
namespace GuidedGrade.ViewModels;
internal sealed record OverallReviewPanelViewModel(IReadOnlyList<SectionFeedback> Pending, Action<SectionFeedback> Approve);
