using System.Diagnostics;

namespace Backstop;

public static class TaskSchedulerService
{
    public const string TaskName = "OpenBackup\\Scheduled backup";
    public static string BuildArguments(string executable, int intervalHours) => $"/Create /TN \"{TaskName}\" /TR \"\\\"{executable}\" --run-scheduled\" /SC HOURLY /MO {Math.Clamp(intervalHours, 1, 168)} /F";
    public static bool Install(string executable, int intervalHours)
    {
        using var process = Process.Start(new ProcessStartInfo("schtasks.exe", BuildArguments(executable, intervalHours)) { CreateNoWindow = true, UseShellExecute = false });
        process?.WaitForExit(10000);
        return process?.ExitCode == 0;
    }
}
