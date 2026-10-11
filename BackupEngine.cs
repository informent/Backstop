using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Backstop;

public sealed record BackupFile(string RelativePath, long Length, string Sha256);
public sealed record BackupSnapshot(string Id, DateTime CreatedUtc, string Source, string Destination, IReadOnlyList<BackupFile> Files);
public sealed record RestorePlan(BackupSnapshot Snapshot, string SnapshotRoot, string TargetRoot, IReadOnlyList<RestoreFile> Files);
public sealed record RestoreFile(string RelativePath, string SourcePath, string TargetPath, long Length, string Sha256);

public static class BackupEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static BackupSnapshot CreateSnapshot(string source, string destination)
    {
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        var sourceRoot = NormalizeDirectory(source);
        var destinationRoot = NormalizeDirectory(destination);
        if (destinationRoot.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The snapshot destination cannot be inside the source folder.");
        Directory.CreateDirectory(destinationRoot);
        var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var finalRoot = Path.Combine(destinationRoot, id);
        var suffix = 1;
        while (Directory.Exists(finalRoot)) finalRoot = Path.Combine(destinationRoot, $"{id}-{suffix++:00}");
        id = Path.GetFileName(finalRoot);
        var workingRoot = Path.Combine(destinationRoot, $".openbackup-{Guid.NewGuid():N}.partial");
        Directory.CreateDirectory(workingRoot);
        try
        {
            var files = new List<BackupFile>();
            foreach (var path in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(path);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException($"Backup stopped at reparse-point file: {Path.GetRelativePath(sourceRoot, path)}");
                var relative = ValidateRelativePath(Path.GetRelativePath(sourceRoot, path));
                var target = ContainedPath(workingRoot, relative, "Source produced an unsafe relative path.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(path, target, false);
                files.Add(new BackupFile(relative, info.Length, Hash(target)));
            }
            var snapshot = new BackupSnapshot(id, DateTime.UtcNow, sourceRoot.TrimEnd(Path.DirectorySeparatorChar), destinationRoot.TrimEnd(Path.DirectorySeparatorChar), files.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray());
            File.WriteAllText(Path.Combine(workingRoot, "manifest.json"), JsonSerializer.Serialize(snapshot, JsonOptions));
            Directory.Move(workingRoot, finalRoot);
            return snapshot;
        }
        catch
        {
            if (Directory.Exists(workingRoot)) Directory.Delete(workingRoot, true);
            throw;
        }
    }

    public static BackupSnapshot PreviewRestore(string snapshotRoot)
    {
        var manifest = Path.Combine(snapshotRoot, "manifest.json");
        if (!File.Exists(manifest)) throw new FileNotFoundException("Snapshot manifest not found", manifest);
        return JsonSerializer.Deserialize<BackupSnapshot>(File.ReadAllText(manifest)) ?? throw new InvalidDataException("Invalid snapshot manifest");
    }

    public static RestorePlan PlanRestore(string snapshotRoot, string target, bool requireEmptyTargets = true)
    {
        var snapshot = PreviewRestore(snapshotRoot);
        var snapshotBase = NormalizeDirectory(snapshotRoot);
        var targetBase = NormalizeDirectory(target);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<RestoreFile>();
        foreach (var file in snapshot.Files)
        {
            var relative = ValidateRelativePath(file.RelativePath);
            if (!seen.Add(relative)) throw new InvalidDataException($"Snapshot contains a duplicate path: {relative}");
            if (file.Length < 0 || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException($"Snapshot metadata is invalid: {relative}");
            var sourcePath = ContainedPath(snapshotBase, relative, "Snapshot contains an unsafe source path.");
            var targetPath = ContainedPath(targetBase, relative, "Snapshot contains an unsafe target path.");
            if (!File.Exists(sourcePath)) throw new InvalidDataException($"Snapshot file is missing: {relative}");
            if (new FileInfo(sourcePath).Length != file.Length || !Hash(sourcePath).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Snapshot integrity check failed: {relative}");
            if (requireEmptyTargets && File.Exists(targetPath)) throw new IOException($"Restore stopped because the file already exists: {relative}");
            files.Add(new RestoreFile(relative, sourcePath, targetPath, file.Length, file.Sha256));
        }
        return new RestorePlan(snapshot, snapshotBase, targetBase, files);
    }

    public static bool VerifySnapshot(string snapshotRoot)
    {
        try { PlanRestore(snapshotRoot, Path.Combine(Path.GetTempPath(), "openbackup-verify-" + Guid.NewGuid().ToString("N")), false); return true; }
        catch (InvalidDataException) { return false; }
        catch (FileNotFoundException) { return false; }
    }

    public static int RestoreSnapshot(string snapshotRoot, string target)
    {
        var plan = PlanRestore(snapshotRoot, target);
        Directory.CreateDirectory(plan.TargetRoot);
        foreach (var file in plan.Files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file.TargetPath)!);
            File.Copy(file.SourcePath, file.TargetPath, false);
        }
        return plan.Files.Count;
    }

    private static string ValidateRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value)) throw new InvalidDataException("Snapshot contains an invalid path.");
        var normalized = value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (normalized.Split(Path.DirectorySeparatorChar).Any(part => part is "" or "." or "..")) throw new InvalidDataException("Snapshot contains an unsafe relative path.");
        return normalized;
    }

    private static string NormalizeDirectory(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    private static string ContainedPath(string root, string relative, string message)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(NormalizeDirectory(root), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(message);
        return path;
    }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
}
