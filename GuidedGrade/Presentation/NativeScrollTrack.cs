using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GuidedGrade.Presentation;

/// <summary>Owns Track's native child slots, which cannot be set through template dependency properties.</summary>
internal sealed class NativeScrollTrack : Track
{
    public NativeScrollTrack()
    {
        DecreaseRepeatButton = new RepeatButton();
        IncreaseRepeatButton = new RepeatButton();
        Thumb = new Thumb();
        DecreaseRepeatButton.SetResourceReference(FrameworkElement.StyleProperty, "ScrollPage");
        IncreaseRepeatButton.SetResourceReference(FrameworkElement.StyleProperty, "ScrollPage");
        Thumb.SetResourceReference(FrameworkElement.StyleProperty, "ScrollThumb");
        UpdateOrientation();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == OrientationProperty && DecreaseRepeatButton is not null) UpdateOrientation();
    }

    private void UpdateOrientation()
    {
        var vertical = Orientation == Orientation.Vertical;
        IsDirectionReversed = vertical;
        DecreaseRepeatButton.Command = vertical ? ScrollBar.PageUpCommand : ScrollBar.PageLeftCommand;
        IncreaseRepeatButton.Command = vertical ? ScrollBar.PageDownCommand : ScrollBar.PageRightCommand;
    }
}
