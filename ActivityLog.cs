using System.IO;
namespace Backstop;
public static class ActivityLog
{
    public static string PathOnDisk => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenBackup", "activity.log");
    public static void Write(string message, string? path = null)
    {
        path ??= PathOnDisk;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
    }
}
