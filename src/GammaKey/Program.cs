using GammaKey.App;
using GammaKey.UI;

namespace GammaKey;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, "Local\\GammaKey.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            MessageBox.Show("GammaKey is already running. Look for its icon by the clock.", "GammaKey");
            return;
        }

        ApplicationConfiguration.Initialize();

        var baseDir = AppContext.BaseDirectory;
        using var controller = new AppController(baseDir);
        controller.Start();
        var mainForm = new MainForm(controller);
        using var tray = new TrayIcon(controller, mainForm);
        if (controller.Settings.ShowWindowOnLaunch) mainForm.ShowAndFocus();
        Application.ApplicationExit += (_, _) => controller.Shutdown();

        Application.Run();
    }
}
