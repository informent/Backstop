using System.Windows;
namespace Backstop;
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Any(a => string.Equals(a, "--run-scheduled", StringComparison.OrdinalIgnoreCase)))
        {
            try { ScheduledRunner.RunIfDue(ProfileStore.Load(), DateTime.Now); } finally { Shutdown(); }
            return;
        }
        ShutdownMode = ShutdownMode.OnMainWindowClose; var window = new MainWindow(); MainWindow = window; window.Show(); window.Activate();
    }
}
