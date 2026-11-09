using GammaKey.App;

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
        Application.ApplicationExit += (_, _) => controller.Shutdown();

        Application.Run();
    }
}
