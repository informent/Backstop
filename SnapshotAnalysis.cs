using System.IO;
using System.Security.Cryptography;

namespace Backstop;

public enum SnapshotChangeKind { Added, Removed, Modified, Unchanged }
public sealed record SnapshotChange(string RelativePath, SnapshotChangeKind Kind, long? BeforeLength, long? AfterLength);
public sealed record SnapshotComparison(string BeforeId, string AfterId, IReadOnlyList<SnapshotChange> Files)
{
    public long ByteDelta => Files.Sum(x => x.AfterLength ?? 0) - Files.Sum(x => x.BeforeLength ?? 0);
    public int Count(SnapshotChangeKind kind) => Files.Count(x => x.Kind == kind);
}
public enum RestoreImpactKind { NewFile, Identical, Conflict, Blocked }
public sealed record RestoreImpact(string RelativePath, RestoreImpactKind Kind, long SnapshotLength, long? ExistingLength);
public sealed record RestorePreview(string SnapshotId, string TargetRoot, IReadOnlyList<RestoreImpact> Files)
{
    public bool CanRestoreWithoutOverwrite => Files.All(x => x.Kind == RestoreImpactKind.NewFile);
    public long BytesToRestore => Files.Sum(x => x.SnapshotLength);
}

public static class SnapshotAnalysis
{
    public static SnapshotComparison Compare(string beforeRoot, string afterRoot)
    {
        var before = Verified(beforeRoot);
        var after = Verified(afterRoot);
        var left = before.Files.ToDictionary(x => x.RelativePath, StringComparer.OrdinalIgnoreCase);
        var right = after.Files.ToDictionary(x => x.RelativePath, StringComparer.OrdinalIgnoreCase);
        var changes = left.Keys.Union(right.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(path =>
            {
                left.TryGetValue(path, out var a); right.TryGetValue(path, out var b);
                var kind = a is null ? SnapshotChangeKind.Added : b is null ? SnapshotChangeKind.Removed :
                    a.Length == b.Length && a.Sha256.Equals(b.Sha256, StringComparison.OrdinalIgnoreCase) ? SnapshotChangeKind.Unchanged : SnapshotChangeKind.Modified;
                return new SnapshotChange(path, kind, a?.Length, b?.Length);
            }).ToArray();
        return new SnapshotComparison(before.Id, after.Id, changes);
    }

    public static RestorePreview Preview(string snapshotRoot, string targetRoot)
    {
        Verified(snapshotRoot);
        var plan = BackupEngine.PlanRestore(snapshotRoot, targetRoot, false);
        var impacts = plan.Files.Select(file =>
        {
            if (HasLinkedAncestor(file.TargetPath) || Directory.Exists(file.TargetPath) || HasFileAncestor(file.TargetPath))
                return new RestoreImpact(file.RelativePath, RestoreImpactKind.Blocked, file.Length, null);
            if (!File.Exists(file.TargetPath)) return new RestoreImpact(file.RelativePath, RestoreImpactKind.NewFile, file.Length, null);
            var length = new FileInfo(file.TargetPath).Length;
            using var stream = File.OpenRead(file.TargetPath);
            var identical = length == file.Length && Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase);
            return new RestoreImpact(file.RelativePath, identical ? RestoreImpactKind.Identical : RestoreImpactKind.Conflict, file.Length, length);
        }).ToArray();
        return new RestorePreview(plan.Snapshot.Id, plan.TargetRoot, impacts);
    }

    private static BackupSnapshot Verified(string root)
    {
        if (HasLinkedAncestor(Path.GetFullPath(root))) throw new InvalidDataException("Snapshot location contains a filesystem link.");
        if (HasLinkedAncestor(Path.Combine(Path.GetFullPath(root), "manifest.json"))) throw new InvalidDataException("Snapshot manifest is a filesystem link.");
        var snapshot = BackupEngine.PreviewRestore(root);
        foreach (var file in snapshot.Files)
            if (HasLinkedAncestor(Path.GetFullPath(Path.Combine(root, file.RelativePath)))) throw new InvalidDataException("Snapshot contains a filesystem link.");
        return BackupEngine.PlanRestore(root, Path.Combine(Path.GetTempPath(), "backstop-analysis-" + Guid.NewGuid().ToString("N")), false).Snapshot;
    }

    private static bool HasLinkedAncestor(string path)
    {
        for (string? current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true; }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        return false;
    }
    private static bool HasFileAncestor(string path)
    {
        for (var current = Path.GetDirectoryName(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if (File.Exists(current)) return true;
        return false;
    }
}
