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
        // Debug aid: the window starts hidden in the tray, so this lets a screenshot run open it.
        if (Environment.GetEnvironmentVariable("GAMMAKEY_SHOW") == "1") mainForm.Show();
        using var tray = new TrayIcon(controller, mainForm);
        Application.ApplicationExit += (_, _) => controller.Shutdown();

        Application.Run();
    }
}
