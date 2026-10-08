using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using GuidedGrade.ViewModels;
using UI_Framework;
using UI_Framework.Wpf;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class FeedbackDialView(FeedbackDialViewModel model)
{
    internal View Build() => HStack(WpfUI.Native(() => new Dial(model)).Id("feedback-dial"), Text(model.Label)).Spacing(16);
    private sealed class Dial : FrameworkElement
    {
        private readonly FeedbackDialViewModel _model;
        internal Dial(FeedbackDialViewModel model)
        {
            _model = model; Width = Height = 90; Focusable = true;
            ToolTip = "Feedback length: drag the dial, use arrow keys, or scroll. 1 very brief; 5 thorough.";
            AutomationProperties.SetName(this, "Feedback length dial");
        }
        protected override void OnRender(DrawingContext dc)
        {
            var center = new Point(45,45);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(34,38,45)), new Pen(IsKeyboardFocused ? Brushes.White : Brushes.SlateGray,2), center,32,32);
            for (var i=1;i<=5;i++)
            {
                var angle=(-135+(i-1)*67.5)*Math.PI/180;
                var point=new Point(45+40*Math.Sin(angle),45-40*Math.Cos(angle));
                dc.DrawEllipse(i==_model.Level.Value?Brushes.DeepSkyBlue:Brushes.SlateGray,null,point,3,3);
            }
            var a=(-135+(Math.Clamp(_model.Level.Value,1,5)-1)*67.5)*Math.PI/180;
            dc.DrawLine(new Pen(Brushes.DeepSkyBlue,4),center,new Point(45+24*Math.Sin(a),45-24*Math.Cos(a)));
        }
        private void Set(int level) { _model.Level.Value=Math.Clamp(level,1,5); InvalidateVisual(); }
        private void PointLevel(Point p)
        {
            var angle=Math.Atan2(p.X-45,45-p.Y)*180/Math.PI;
            Set((int)Math.Round((Math.Clamp(angle,-135,135)+135)/67.5)+1);
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { Focus(); CaptureMouse(); PointLevel(e.GetPosition(this)); e.Handled=true; }
        protected override void OnMouseMove(MouseEventArgs e) { if(IsMouseCaptured) PointLevel(e.GetPosition(this)); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { ReleaseMouseCapture(); }
        protected override void OnMouseWheel(MouseWheelEventArgs e) { Set(_model.Level.Value+Math.Sign(e.Delta)); e.Handled=true; }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(e.Key is Key.Right or Key.Up) Set(_model.Level.Value+1);
            else if(e.Key is Key.Left or Key.Down) Set(_model.Level.Value-1);
            else if(e.Key==Key.Home) Set(1); else if(e.Key==Key.End) Set(5); else { base.OnKeyDown(e); return; }
            e.Handled=true;
        }
    }
}
