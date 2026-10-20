using System.Windows;
using System.Windows.Controls;
using GuidedGrade.Models;
using UI_Framework;
using UI_Framework.Wpf;

namespace GuidedGrade.Presentation;

// Retain native ListBox selection/virtualization; render each row with the pinned framework.
public sealed class StudentGradeRow : ContentControl
{
    public static readonly DependencyProperty PresenterProperty = DependencyProperty.Register(
        nameof(Presenter), typeof(Func<Student, View>), typeof(StudentGradeRow), new PropertyMetadata(null,
            (sender, _) => ((StudentGradeRow)sender).Mount()));
    public Func<Student, View>? Presenter
    {
        get => (Func<Student, View>?)GetValue(PresenterProperty);
        set => SetValue(PresenterProperty, value);
    }
    private ViewHost? _host;
    public StudentGradeRow()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => Mount();
        DataContextChanged += (_, _) => Mount();
        Unloaded += (_, _) => { _host?.Dispose(); _host = null; Content = null; };
    }
    private void Mount()
    {
        _host?.Dispose(); _host = null;
        Content = DataContext is Student student && Presenter is { } presenter
            ? _host = ReviewTheme.Host(() => presenter(student)) : null;
    }
}
