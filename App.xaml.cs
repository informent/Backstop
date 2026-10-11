using System.Windows;
namespace Backstop;
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (AnalysisCommand.IsAnalysisCommand(e.Args))
        {
            try { AnalysisCommand.Run(e.Args); Shutdown(0); }
            catch (Exception) { Shutdown(2); }
            return;
        }
        if (e.Args.Any(a => string.Equals(a, "--run-scheduled", StringComparison.OrdinalIgnoreCase)))
        {
            try { ScheduledRunner.RunIfDue(ProfileStore.Load(), DateTime.Now); } finally { Shutdown(); }
            return;
        }
        ShutdownMode = ShutdownMode.OnMainWindowClose; var window = new MainWindow(); MainWindow = window; window.Show(); window.Activate();
    }
}
