using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backstop;

public static class AnalysisCommand
{
    public static bool IsAnalysisCommand(string[] args) => args.Length > 0 && args[0] is "--compare" or "--preview-restore";
    public static void Run(string[] args)
    {
        if (args.Length != 5 || !IsAnalysisCommand(args) || args[3] != "--report" || args.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Use --compare BEFORE AFTER --report REPORT.json or --preview-restore SNAPSHOT TARGET --report REPORT.json.");
        var path = Path.GetFullPath(args[4]);
        if (File.Exists(path) || Directory.Exists(path)) throw new IOException("The report destination already exists.");
        object report = args[0] == "--compare" ? SnapshotAnalysis.Compare(args[1], args[2]) : SnapshotAnalysis.Preview(args[1], args[2]);
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var json = JsonSerializer.SerializeToUtf8Bytes(report, report.GetType(), options);
        var temp = Path.Combine(Path.GetDirectoryName(path)!, ".backstop-report-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(json); stream.Flush(true); }
            File.Move(temp, path, false);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
