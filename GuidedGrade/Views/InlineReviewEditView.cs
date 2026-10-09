using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class InlineReviewEditView(InlineReviewEditViewModel model, Action save, Action cancel)
{
    internal View Build() => VStack(Text(model.Title).FontSize(20),
        model.Files.Count > 0 ? VStack(Text("Comment file"), Picker(model.Files.Select(file => System.IO.Path.GetRelativePath(System.IO.Path.GetDirectoryName(model.Files[0])!, file)).ToArray(), model.FileIndex).IsEnabled(model.CanMove).AccessibilityLabel("Comment destination file")).Spacing(5) : Text(""),
        Text("Your inline comment"),
        TextEditor(new Binding<string>(() => model.Comment.Value, value => model.Comment.Value = value)).Height(180).AccessibilityLabel("Inline comment text"),
        HStack(VStack(Text($"{model.PointsLabel} (0–{model.Maximum})"), TextField(model.Points).AccessibilityLabel("Points awarded")).Spacing(5),
            VStack(Text("Code line"), TextField(model.Line).AccessibilityLabel("Comment line").IsEnabled(model.CanMove)).Spacing(5)).Spacing(12),
        Text(model.Error.Value).Foreground("#FFABAB"),
        HStack(Button("Save changes", save).ButtonStyle(ButtonStyleKind.Primary), Button("Cancel", cancel)).Spacing(10)
    ).Spacing(12).Padding(20);
}
