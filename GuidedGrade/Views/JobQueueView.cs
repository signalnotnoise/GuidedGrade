using UI_Framework;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class JobQueueView(JobQueueViewModel model)
{ internal View Build() {
        var rows = model.Rows.Value;
        var selected = rows.FirstOrDefault(row => row.Key == model.SelectedKey.Value);
        return Scroll(VStack(
            Text("Job queue").FontSize(20),
            Button("Design batch review", model.Batch),
            Button("Open selected student's saved review", model.OpenReview),
            TextEditor(new Binding<string>(() => model.Progress.Value, _ => { })).Height(100).IsReadOnly(true).UndoLimit(0).AccessibilityLabel("Batch progress"),
            Text("Assignments run before general AI jobs. Active requests finish or cancel before the next starts.").FontSize(13),
            TextEditor(model.Prompt).Height(80).MaxLength(32000).AccessibilityLabel("Task for AI provider"),
            HStack(Button("Queue task", model.QueueTask).IsEnabled(!string.IsNullOrWhiteSpace(model.Prompt.Value)),
                Button("Clear finished", model.ClearFinished)).Spacing(8),
            VirtualList(rows.Select(row => VStack(
                Text(row.Title), Text(row.Description).FontSize(12),
                HStack(Button("Details", () => model.SelectedKey.Value = row.Key),
                    Button("Cancel job", () => model.Cancel(row)).IsEnabled(row.CanCancel)).Spacing(8)
            ).Spacing(6).Padding(8).Background(row.Key == model.SelectedKey.Value ? "#344457" : ReviewTheme.Tokens.Surface).Id(row.Key)), model.Height.Value),
            Text("Selected job result"),
            TextEditor(new Binding<string>(() => selected?.Job.Result ?? "", _ => { })).Height(180).IsReadOnly(true).UndoLimit(0).AccessibilityLabel("Selected job result")
        ).Spacing(10).Padding(14));
    }
}