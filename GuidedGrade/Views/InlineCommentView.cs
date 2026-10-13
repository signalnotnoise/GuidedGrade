using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;

namespace GuidedGrade.Views;

internal sealed class InlineCommentView(InlineCommentViewModel model)
{
    internal View Build()
    {
        var children = new List<View>
        {
            HStack(new GuidedGrade.Views.CommentPlacementView(new GuidedGrade.ViewModels.CommentPlacementViewModel(
                model.Feedback.IsPinned, !model.EarlierReview, model.BeginDrag, model.TogglePin)).Build(),
            Button($"{(model.Expanded.Value ? "−" : "+")} {(model.Approved.Value ? "Approved" : "Review")}: {model.Feedback.SectionName}{(model.Feedback.IsOverallReview ? "" : $" · {model.Feedback.SuggestedScore} pts")}",
                () => model.Expanded.Value = !model.Expanded.Value).ButtonStyle(ButtonStyleKind.Quiet).Id("header")).Spacing(4)
        };
        if (model.EarlierReview)
            children.Add(Text("Earlier review · assignment not recorded").FontSize(12).Foreground("#FCCF31"));
        if (model.Feedback.RubricReview is { LocationResolved: false })
            children.Add(Text("Location needs review — Modify to choose a line.").FontSize(12).Foreground("#FCCF31"));
        if (model.Expanded.Value)
        {
            if (model.Feedback.Strengths.Count > 0)
                children.Add(VStack(Text("Strengths").Foreground("#4EC9B0"), VStack(model.Feedback.Strengths.Select(value => Text("• " + value)).ToArray()).Spacing(4)).Spacing(6).Id("strengths"));
            if (model.Feedback.Issues.Count > 0)
                children.Add(VStack(Text("Issues").Foreground("#FCCF31"), VStack(model.Feedback.Issues.Select(value => Text("• " + value)).ToArray()).Spacing(4)).Spacing(6).Id("issues"));
            if (!string.IsNullOrWhiteSpace(model.Feedback.SuggestedCode))
                children.Add(VStack(Text("Suggested fix").Foreground("#569CD6"),
                    TextEditor(new Binding<string>(() => model.Feedback.SuggestedCode, _ => { })).IsReadOnly(true).UndoLimit(0).Height(160).AccessibilityLabel("Suggested code")
                ).Spacing(6).Id("code"));
            if (!string.IsNullOrWhiteSpace(model.Feedback.Explanation))
                children.Add(VStack(Text("Explanation").Foreground("#CE9178"), Text(model.Feedback.Explanation)).Spacing(6).Id("explanation"));
            if (!model.EarlierReview)
                children.Add(AdaptiveGrid(100,
                    Button("Approve", model.Approve).AccessibilityLabel("Approve inline comment").ButtonStyle(ButtonStyleKind.Primary).IsEnabled(!model.Approved.Value && model.Feedback.RubricReview is not { LocationResolved: false }),
                    Button("Modify", model.Modify).AccessibilityLabel("Modify inline comment"),
                    Button("Reject", model.Reject).AccessibilityLabel("Reject inline comment")).Spacing(8).Id("actions"));
        }
        // The editor overlay caps cards at 400px. Scroll long reviews instead of clipping actions.
        var content = model.Expanded.Value
            ? new[] { children[0], Scroll(VStack(children.Skip(1).ToArray()).Spacing(10)).Height(290).Id("details") }
            : children.ToArray();
        return VStack(content).Spacing(8).Padding(6).Background(ReviewTheme.Tokens.Surface).CornerRadius(6);
    }

}
