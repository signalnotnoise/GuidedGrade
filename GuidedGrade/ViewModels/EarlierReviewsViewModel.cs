using GuidedGrade.Models;
namespace GuidedGrade.ViewModels;
internal sealed class EarlierReviewsViewModel(SectionFeedback[] comments)
{ internal SectionFeedback[] Comments { get; } = comments; }
