using System.Windows;
using GuidedGrade.Models;
using UI_Framework;

namespace GuidedGrade.ViewModels;

internal sealed class InlineCommentViewModel
{
    internal SectionFeedback Feedback { get; }
    internal bool EarlierReview { get; }
    internal State<bool> Expanded { get; } = new(false);
    internal State<bool> Approved { get; }
    internal Action Approve { get; }
    internal Action Modify { get; }
    internal Action Reject { get; }
    internal Action TogglePin { get; }
    internal Action<DependencyObject> BeginDrag { get; }

    internal InlineCommentViewModel(SectionFeedback feedback, bool earlierReview, Action approve,
        Action modify, Action reject, Action togglePin, Action<DependencyObject> beginDrag)
    {
        Feedback = feedback;
        EarlierReview = earlierReview;
        Approved = new(feedback.ReviewStatus == FeedbackReviewStatus.Approved);
        Approve = approve;
        Modify = modify;
        Reject = reject;
        TogglePin = togglePin;
        BeginDrag = beginDrag;
    }

    internal void MarkApproved()
    {
        Feedback.ReviewStatus = FeedbackReviewStatus.Approved;
        Approved.Value = true;
        Expanded.Value = false;
    }
}
