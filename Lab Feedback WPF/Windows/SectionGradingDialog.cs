using System.Windows;
using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Presentation;
using UI_Framework;
using static UI_Framework.UI;

namespace Lab_Feedback_WPF.Windows;

public sealed class SectionGradingDialog : ReviewWindow
{
    private readonly State<string> _name = new("");
    private readonly List<RubricItem> _items;
    private readonly State<bool>[] _selected;
    public string SectionName => _name.Value;
    public List<RubricItem> SelectedRubricItems { get; private set; } = new();

    public SectionGradingDialog(List<RubricItem> allRubricItems)
    {
        Title = "Grade selected section"; Width = 580; Height = 540; MinWidth = 420; MinHeight = 320;
        _items = allRubricItems.ToList();
        _selected = _items.Select(_ => new State<bool>(false)).ToArray();
        ShowView(() => Scroll(VStack(
            Text("Grade selected section").FontSize(22),
            Text("Section name"), TextField(_name).AccessibilityLabel("Section name"),
            Text("Rubric items"),
            VStack(_items.Select((item, index) => Toggle($"{item.Name} ({item.MaxPoints} pts)", _selected[index]).Id(index.ToString())).ToArray()).Spacing(10),
            HStack(Button("Grade", Grade).ButtonStyle(ButtonStyleKind.Primary), Button("Cancel", () => DialogResult = false)).Spacing(10)
        ).Spacing(14).Padding(20)));
    }

    private void Grade()
    {
        if (string.IsNullOrWhiteSpace(_name.Value)) { MessageBox.Show(this, "Enter a section name.", "Grade section"); return; }
        SelectedRubricItems = _items.Where((_, index) => _selected[index].Value).ToList();
        if (SelectedRubricItems.Count == 0) { MessageBox.Show(this, "Select at least one rubric item.", "Grade section"); return; }
        DialogResult = true;
    }
}
