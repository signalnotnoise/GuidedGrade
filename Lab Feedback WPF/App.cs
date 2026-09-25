using System.Windows;
namespace Lab_Feedback_WPF;
public sealed class App : Application
{
    [STAThread]
    public static void Main()
    {
        var application = new App { ShutdownMode = ShutdownMode.OnMainWindowClose };
        application.Run(new MainWindow());
    }
}
