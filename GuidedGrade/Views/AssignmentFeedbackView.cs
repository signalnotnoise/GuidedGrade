using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class AssignmentFeedbackView(AssignmentFeedbackViewModel model)
{
    internal View Build() => VStack(Text("Feedback preferences").FontSize(21),
        new ReviewerRoleView(model.ReviewerRole).Build(),
        Text("Feedback length"),
        new FeedbackDialView(new FeedbackDialViewModel(model.Detail)).Build(),
        Text("Reading level"),
        UI_Framework.Wpf.WpfUI.Native(() =>
        {
            var menu = new System.Windows.Controls.Menu();
            var item = new System.Windows.Controls.MenuItem { Header = model.ReadingLevel.Value, ToolTip = "Choose the language level for feedback" };
            foreach (var level in new[] { "Middle school", "High school", "College", "Technical" })
            {
                var option = new System.Windows.Controls.MenuItem { Header = level, IsCheckable = true, IsChecked = model.ReadingLevel.Value == level };
                option.Click += (_, _) => model.ReadingLevel.Value = level; item.Items.Add(option);
            }
            menu.Items.Add(item); return menu;
        }).Id("feedback-reading-" + model.ReadingLevel.Value),
        Text("Language and length affect explanations only; rubric standards and scores stay the same."),
        Toggle("Address as you and your; never use a name", model.Direct),
        Toggle("Score breakdown", model.Breakdown), Toggle("Deductions applied", model.Deductions),
        Toggle("Final grade", model.Grade), Toggle("Constructive feedback paragraph", model.Feedback),
        Text("Findings must cite source evidence. Missing context and untested runtime behavior remain unverified.")).Spacing(16);
}
