using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Search;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using UI_Framework.Wpf;
using GuidedGrade.Presentation;
using GuidedGrade.Views;

namespace GuidedGrade.ViewModels;

// AvalonEdit owns match navigation/highlights; the framework owns the visible search bar.
internal sealed class DocumentSearchViewModel : IDisposable
{
    internal TextEditor Editor { get; }
    internal SearchPanel Panel { get; }
    internal event Action? Changed;
    internal string ActivePattern => Panel.IsClosed ? "" : Panel.SearchPattern ?? "";
    private readonly ViewHost _searchHost;

    internal DocumentSearchViewModel(TextEditor editor, string name)
    {
        Editor = editor;
        Panel = SearchPanel.Install(editor);
        Panel.MarkerBrush = new SolidColorBrush(Color.FromArgb(190, 255, 193, 7));
        AutomationProperties.SetName(Panel, name + " search");
        _searchHost = ReviewTheme.Host(new SearchBarView(new SearchBarViewModel(this)).Build);
        _searchHost.Width = 370; _searchHost.Height = 60;
        var root = new FrameworkElementFactory(typeof(Grid));
        // AvalonEdit uses this named field for validation and keyboard handling.
        var field = new FrameworkElementFactory(typeof(TextBox), "PART_searchTextBox");
        field.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        field.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SearchPattern")
        { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        root.AppendChild(field);
        var content = new FrameworkElementFactory(typeof(ContentControl));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        content.SetBinding(ContentControl.ContentProperty, new System.Windows.Data.Binding { Source = _searchHost });
        root.AppendChild(content);
        Panel.Template = new ControlTemplate(typeof(SearchPanel)) { VisualTree = root };
        Panel.Margin = new Thickness(8);




        _searchHost.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) { Close(); args.Handled = true; }
            else if (args.Key == Key.Enter)
            { if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) Panel.FindPrevious(); else Panel.FindNext(); args.Handled = true; }
        };
        _searchHost.Loaded += (_, _) => SearchBarNativeAdapter.FocusQuery(_searchHost);
        Panel.SearchOptionsChanged += (_, _) => Changed?.Invoke();
        Panel.IsVisibleChanged += (_, _) => Changed?.Invoke();
    }

    internal void Open()
    {
        if (!string.IsNullOrWhiteSpace(Editor.SelectedText) && !Editor.SelectedText.Contains('\n'))
            Panel.SearchPattern = Editor.SelectedText;
        Panel.Open();
        SearchBarNativeAdapter.FocusQuery(_searchHost);
        Changed?.Invoke();
    }

    internal void Close()
    {
        Panel.Close();
        Changed?.Invoke();
    }

    public void Dispose() { Panel.Uninstall(); _searchHost.Dispose(); }
}
