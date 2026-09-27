using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using GuidedGrade;
using GuidedGrade.Models;
using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UI_Framework.Wpf;

namespace GuidedGrade.Tests.Services;

[TestClass]
[DoNotParallelize]
public class FileTreeVirtualizationTests
{
    [TestMethod]
    public void RecyclingRetainsNestedStateAndOpenDocument()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(new Action(async () =>
            {
                var directory = Path.Combine(Path.GetTempPath(), "FileTreeVirtualization-" + Guid.NewGuid());
                Directory.CreateDirectory(directory);
                MainWindow? window = null;
                try
                {
                    var database = Path.Combine(directory, "test.db");
                    window = new MainWindow(new AssignmentPersistenceService(database), new CommentPersistenceService(database));
                    var root = (ViewHost)window.Content;
                    window.Content = null;
                    var tree = window.fileTreeView;
                    Assert.IsTrue(VirtualizingPanel.GetIsVirtualizing(tree));
                    Assert.AreEqual(VirtualizationMode.Recycling, VirtualizingPanel.GetVirtualizationMode(tree));
                    for (var i = 0; i < 1000; i++) tree.Items.Add(new FileSystemItem(Path.Combine(directory, $"Synthetic{i}.cs"), false));
                    using var source = new HwndSource(new HwndSourceParameters("File tree regression")
                    { Width = 1440, Height = 960, PositionX = -10000, PositionY = -10000, WindowStyle = unchecked((int)0x80000000) });
                    source.RootVisual = root;
                    async Task Layout()
                    {
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        root.Measure(new Size(1440, 960)); root.Arrange(new Rect(0, 0, 1440, 960)); root.UpdateLayout();
                    }
                    await Layout();
                    Assert.IsTrue(Enumerable.Range(0, 1000).Count(i => tree.ItemContainerGenerator.ContainerFromIndex(i) is not null) < 200);
                    await FileTreeChecks.Run(tree, Layout, directory, true, window.codeEditor);
                    source.RootVisual = null;
                    window.Close();
                    Assert.ThrowsException<ObjectDisposedException>(() => root.Refresh());
                }
                catch (Exception error) { failure = error; }
                finally
                {
                    window?.Close();
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    Directory.Delete(directory, true);
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                }
            }));
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(45)), "Tree recycling regression timed out.");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
