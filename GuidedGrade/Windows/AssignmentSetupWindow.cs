using System.Windows;
using GuidedGrade.Models;
using GuidedGrade.Services;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using GuidedGrade.Views;
namespace GuidedGrade.Windows;
public sealed class AssignmentSetupWindow : ReviewWindow
{
    private readonly AssignmentPersistenceService _persistence;
    internal AssignmentSetupViewModel Model { get; }
    public GradingAssignment Assignment { get; private set; } = new();
    public AssignmentSetupWindow() : this(new AssignmentPersistenceService()) { }
    public AssignmentSetupWindow(GradingAssignment? existingAssignment) : this(new AssignmentPersistenceService(), existingAssignment) { }
    internal AssignmentSetupWindow(AssignmentPersistenceService persistence, GradingAssignment? initial = null)
    {
        _persistence = persistence; Model = new(persistence, initial);
        Title = "Assignment setup"; Width = 820; Height = 760; MinWidth = 600; MinHeight = 600;
        var view = new AssignmentSetupView(Model, LoadSaved, ImportPrompt, ImportRubric, Save, () => DialogResult = false);
        ShowView(view.Build);
    }
    private void LoadSaved()
    {
        var model = new SavedAssignmentsViewModel(_persistence);
        var dialog = new SetupDialog { Owner = this, Title = "Load assignment", Width = 580, Height = 270 };
        var view = new SavedAssignmentsView(model, () => { if (model.Selected != null) { Model.Load(model.Selected); dialog.DialogResult = true; } }, () => dialog.DialogResult = false);
        dialog.SetView(view.Build); dialog.ShowDialog();
    }
    private void ImportPrompt()
    {
        var model = new AssignmentImportViewModel();
        var dialog = new SetupDialog { Owner = this, Title = "Import grading prompt", Width = 780, Height = 800 };
        var view = new AssignmentImportView(model, () => { if (model.CanApply) { Model.Import(model.Parsed!); dialog.DialogResult = true; } }, () => dialog.DialogResult = false);
        dialog.SetView(view.Build); dialog.ShowDialog();
    }
    private void ImportRubric()
    {
        var dialog = new RubricImportWindow { Owner = this };
        if (dialog.ShowDialog() == true) foreach (var item in dialog.Items) Model.Rubric.Rows.Add(new RubricEditorRow(item.Name, item.MaxPoints));
    }
    private sealed class SetupDialog : ReviewWindow
    {
        internal void SetView(Func<UI_Framework.View> build) => ShowView(build);
    }
    private void Save()
    {
        try { Assignment = Model.Save(); DialogResult = true; }
        catch (Exception ex) { Model.Error.Value = ex.Message; }
    }
}
