using System.Windows;

namespace ERSC.Launcher;

public static class App
{
    [STAThread]
    public static void Main()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        application.Run(new MainWindow());
    }
}
