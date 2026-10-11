using Backstop;
var root = Path.Combine(Path.GetTempPath(), "backstop-analysis-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var source = Path.Combine(root, "source"); var snapshots = Path.Combine(root, "snapshots");
    Directory.CreateDirectory(source);
    File.WriteAllText(Path.Combine(source, "same.txt"), "same");
    File.WriteAllText(Path.Combine(source, "changed.txt"), "old!");
    File.WriteAllText(Path.Combine(source, "removed.txt"), "old");
    var before = BackupEngine.CreateSnapshot(source, snapshots);
    File.WriteAllText(Path.Combine(source, "changed.txt"), "new!");
    File.Delete(Path.Combine(source, "removed.txt"));
    File.WriteAllText(Path.Combine(source, "added.txt"), "added");
    Directory.CreateDirectory(Path.Combine(source, "nested"));
    File.WriteAllText(Path.Combine(source, "nested", "child.txt"), "child");
    var after = BackupEngine.CreateSnapshot(source, snapshots);
    var a = Path.Combine(snapshots, before.Id); var b = Path.Combine(snapshots, after.Id);
    var result = SnapshotAnalysis.Compare(a, b);
    Assert(result.Count(SnapshotChangeKind.Added) == 2, "Added files");
    Assert(result.Count(SnapshotChangeKind.Removed) == 1, "Removed files");
    Assert(result.Count(SnapshotChangeKind.Modified) == 1, "Same-size content change");
    Assert(result.Count(SnapshotChangeKind.Unchanged) == 1, "Unchanged file");
    Assert(result.ByteDelta == 7, "Byte delta");
    Assert(SnapshotAnalysis.Compare(a, a).Files.All(x => x.Kind == SnapshotChangeKind.Unchanged), "Self comparison");
    var target = Path.Combine(root, "target");
    var fresh = SnapshotAnalysis.Preview(b, target);
    Assert(fresh.CanRestoreWithoutOverwrite && !Directory.Exists(target), "Preview must not create target");
    Directory.CreateDirectory(target);
    File.WriteAllText(Path.Combine(target, "same.txt"), "same");
    File.WriteAllText(Path.Combine(target, "changed.txt"), "keep");
    Directory.CreateDirectory(Path.Combine(target, "added.txt"));
    File.WriteAllText(Path.Combine(target, "nested"), "block parent");
    var preview = SnapshotAnalysis.Preview(b, target);
    Assert(preview.Files.Single(x => x.RelativePath == "same.txt").Kind == RestoreImpactKind.Identical, "Identical target");
    Assert(preview.Files.Single(x => x.RelativePath == "changed.txt").Kind == RestoreImpactKind.Conflict, "Changed target");
    Assert(preview.Files.Count(x => x.Kind == RestoreImpactKind.Blocked) == 2, "Directory and file-parent blockers");
    Assert(!preview.CanRestoreWithoutOverwrite && File.ReadAllText(Path.Combine(target, "changed.txt")) == "keep", "Existing content preserved");
    var report = Path.Combine(root, "comparison.json");
    AnalysisCommand.Run(new[] { "--compare", a, b, "--report", report });
    var reportBytes = File.ReadAllBytes(report);
    Assert(File.ReadAllText(report).Contains("Modified"), "Named JSON classifications");
    try { AnalysisCommand.Run(new[] { "--compare", a, b, "--report", report }); throw new Exception("Overwrote existing report"); } catch (IOException) { }
    Assert(File.ReadAllBytes(report).SequenceEqual(reportBytes), "Report destination preserved");
    var impactReport = Path.Combine(root, "impact.json");
    AnalysisCommand.Run(new[] { "--preview-restore", b, target, "--report", impactReport });
    Assert(File.ReadAllText(impactReport).Contains("Blocked"), "Restore report output");
    try { AnalysisCommand.Run(new[] { "--compare", a }); throw new Exception("Accepted invalid arguments"); } catch (ArgumentException) { }
    File.AppendAllText(Path.Combine(b, "changed.txt"), "tampered");
    Assert(Rejects(() => SnapshotAnalysis.Compare(a, b)), "Tampered comparison rejected");
    Assert(Rejects(() => SnapshotAnalysis.Preview(b, target)), "Tampered preview rejected");
    Console.WriteLine("PASS: snapshot change classification, byte delta, integrity checks, restore impacts and read-only behavior");
}
finally
{
    var full = Path.GetFullPath(root);
    var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("backstop-analysis-tests-")) throw new Exception("Unsafe test cleanup path");
    Directory.Delete(full, true);
}
static void Assert(bool ok, string name) { if (!ok) throw new Exception(name); }
static bool Rejects(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }
