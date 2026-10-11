using System.IO;
using System.Text.Json;

namespace Backstop;

public sealed record RetentionCandidate(string SnapshotId, string Path, DateTime CreatedUtc, long Bytes);
public sealed record RetentionPlan(int Keep, IReadOnlyList<RetentionCandidate> Retained, IReadOnlyList<RetentionCandidate> Remove, IReadOnlyList<string> Skipped);

public static class RetentionEngine
{
    public static RetentionPlan Plan(string destination, int keep)
    {
        if (keep < 1) throw new ArgumentOutOfRangeException(nameof(keep), "At least one snapshot must be retained.");
        if (!Directory.Exists(destination)) return new RetentionPlan(keep, Array.Empty<RetentionCandidate>(), Array.Empty<RetentionCandidate>(), Array.Empty<string>());
        var root = Normalize(destination); var valid = new List<RetentionCandidate>(); var skipped = new List<string>();
        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            try
            {
                EnsureDirectSafeChild(root, folder);
                var info = new DirectoryInfo(folder);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Reparse-point snapshot folders are not eligible for retention.");
                var snapshot = BackupEngine.PreviewRestore(folder);
                if (!string.Equals(snapshot.Id, info.Name, StringComparison.Ordinal)) throw new InvalidDataException("Manifest snapshot ID does not match its folder name.");
                valid.Add(new RetentionCandidate(snapshot.Id, info.FullName, snapshot.CreatedUtc, MeasureBytes(info.FullName)));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException) { skipped.Add($"{folder}: {ex.Message}"); }
        }
        var ordered = valid.OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.SnapshotId, StringComparer.Ordinal).ToArray();
        return new RetentionPlan(keep, ordered.Take(keep).ToArray(), ordered.Skip(keep).ToArray(), skipped);
    }

    public static int Apply(RetentionPlan plan, string destination)
    {
        ArgumentNullException.ThrowIfNull(plan); var root = Normalize(destination); var removed = 0;
        foreach (var candidate in plan.Remove)
        {
            EnsureDirectSafeChild(root, candidate.Path);
            var info = new DirectoryInfo(candidate.Path);
            if (!info.Exists) continue;
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException($"Retention stopped because a candidate became a reparse point: {candidate.SnapshotId}");
            var snapshot = BackupEngine.PreviewRestore(candidate.Path);
            if (!string.Equals(snapshot.Id, info.Name, StringComparison.Ordinal) || !string.Equals(snapshot.Id, candidate.SnapshotId, StringComparison.Ordinal)) throw new InvalidDataException($"Retention candidate changed after planning: {candidate.SnapshotId}");
            Directory.Delete(info.FullName, true); removed++;
        }
        return removed;
    }

    public static int RemoveOlderSnapshots(string destination, int keep) => Apply(Plan(destination, keep), destination);

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    private static void EnsureDirectSafeChild(string root, string candidate)
    {
        var full = Normalize(candidate); var parent = Directory.GetParent(full)?.FullName;
        if (parent is null || !Normalize(parent).Equals(Normalize(root), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Retention candidate is not a direct child of the snapshot library.");
    }
    private static long MeasureBytes(string root)
    {
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) { var info = new FileInfo(file); if ((info.Attributes & FileAttributes.ReparsePoint) == 0) total += info.Length; }
        return total;
    }
}
