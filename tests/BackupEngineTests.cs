using Backstop;
var root = Path.Combine(Path.GetTempPath(), "openbackup-" + Guid.NewGuid().ToString("N")); var source = Path.Combine(root, "source"); var destination = Path.Combine(root, "snapshots"); Directory.CreateDirectory(source); File.WriteAllText(Path.Combine(source, "notes.txt"), "important");
var snapshot = BackupEngine.CreateSnapshot(source, destination); if (snapshot.Files.Count != 1 || !BackupEngine.VerifySnapshot(Path.Combine(destination, snapshot.Id))) throw new Exception("Fresh snapshot did not verify.");
if (!Throws(() => BackupEngine.CreateSnapshot(source, Path.Combine(source, "nested")))) throw new Exception("Nested destination was not rejected.");
var snapshotRoot = Path.Combine(destination, snapshot.Id);
var plan = BackupEngine.PlanRestore(snapshotRoot, Path.Combine(root, "planned")); if (plan.Files.Count != 1 || plan.Files[0].RelativePath != "notes.txt") throw new Exception("Restore preflight plan is incorrect.");
var restore = Path.Combine(root, "restore"); if (BackupEngine.RestoreSnapshot(snapshotRoot, restore) != 1 || File.ReadAllText(Path.Combine(restore, "notes.txt")) != "important") throw new Exception("Restore did not recreate the original file.");
if (Directory.EnumerateDirectories(Path.GetDirectoryName(restore)!, ".backstop-restore-*.partial").Any()) throw new Exception("Successful restore left a staging directory behind.");
var collisionTarget = Path.Combine(root, "collision"); Directory.CreateDirectory(collisionTarget); File.WriteAllText(Path.Combine(collisionTarget, "notes.txt"), "keep me");
if (!ThrowsIo(() => BackupEngine.RestoreSnapshot(snapshotRoot, collisionTarget)) || File.ReadAllText(Path.Combine(collisionTarget, "notes.txt")) != "keep me") throw new Exception("Restore collision preflight modified an existing file.");
var manifestPath = Path.Combine(snapshotRoot, "manifest.json"); var originalManifest = File.ReadAllText(manifestPath);
File.WriteAllText(manifestPath, originalManifest.Replace("notes.txt", "..\\\\escape.txt"));
if (!ThrowsData(() => BackupEngine.PlanRestore(snapshotRoot, Path.Combine(root, "unsafe")))) throw new Exception("Manifest traversal was not rejected.");
File.WriteAllText(manifestPath, originalManifest);
// Every restore target must be preflighted before any file is copied.
Directory.CreateDirectory(Path.Combine(source, "nested")); File.WriteAllText(Path.Combine(source, "nested", "later.txt"), "later");
var multi = BackupEngine.CreateSnapshot(source, destination); var multiRoot = Path.Combine(destination, multi.Id);
var mergeTarget = Path.Combine(root, "merge-target"); Directory.CreateDirectory(mergeTarget); File.WriteAllText(Path.Combine(mergeTarget, "unrelated.txt"), "preserve");
if (BackupEngine.RestoreSnapshot(multiRoot, mergeTarget) != 2 || File.ReadAllText(Path.Combine(mergeTarget, "unrelated.txt")) != "preserve" || File.ReadAllText(Path.Combine(mergeTarget, "nested", "later.txt")) != "later") throw new Exception("Staged restore did not preserve unrelated destination files.");
if (Directory.EnumerateDirectories(Path.GetDirectoryName(mergeTarget)!, ".backstop-restore-*.partial").Any()) throw new Exception("Restore left a staging directory behind.");
var blocked = Path.Combine(root, "blocked"); Directory.CreateDirectory(blocked); Directory.CreateDirectory(Path.Combine(blocked, "notes.txt"));
if (!ThrowsIo(() => BackupEngine.RestoreSnapshot(multiRoot, blocked)) || Directory.Exists(Path.Combine(blocked, "nested"))) throw new Exception("Directory collision was not preflighted.");
Directory.Delete(Path.Combine(blocked, "notes.txt")); File.WriteAllText(Path.Combine(blocked, "nested"), "keep parent");
if (!ThrowsIo(() => BackupEngine.RestoreSnapshot(multiRoot, blocked)) || File.Exists(Path.Combine(blocked, "notes.txt"))) throw new Exception("File parent collision was not preflighted.");
var reserved = Path.Combine(root, "reserved"); Directory.CreateDirectory(reserved); File.WriteAllText(Path.Combine(reserved, "manifest.json"), "private source");
var reservedOutput = Path.Combine(root, "reserved-output");
if (!ThrowsData(() => BackupEngine.CreateSnapshot(reserved, reservedOutput)) || Directory.GetFileSystemEntries(reservedOutput).Length != 0 || File.ReadAllText(Path.Combine(reserved, "manifest.json")) != "private source") throw new Exception("Reserved metadata collision corrupted a snapshot.");
File.WriteAllText(manifestPath, "{\"Files\":null}"); if (BackupEngine.VerifySnapshot(snapshotRoot)) throw new Exception("Null file list verified.");
File.WriteAllText(manifestPath, "{"); if (BackupEngine.VerifySnapshot(snapshotRoot)) throw new Exception("Malformed manifest verified.");
File.WriteAllText(manifestPath, originalManifest);
foreach (var unsafePath in new[] { "notes.txt:stream", "notes.txt.", "notes.txt " })
{
    File.WriteAllText(manifestPath, originalManifest.Replace("notes.txt", unsafePath));
    if (BackupEngine.VerifySnapshot(snapshotRoot)) throw new Exception("Windows path alias verified.");
}
File.WriteAllText(manifestPath, originalManifest);
var outside = Path.Combine(root, "outside"); Directory.CreateDirectory(outside); File.WriteAllText(Path.Combine(outside, "private.txt"), "private");
var link = Path.Combine(source, "linked");
try
{
    Directory.CreateSymbolicLink(link, outside);
}
catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { Console.WriteLine("INFO: symbolic link creation unavailable"); }
if (Directory.Exists(link))
{
    if (!ThrowsData(() => BackupEngine.CreateSnapshot(source, destination))) throw new Exception("Linked source folder was followed.");
    if (!ThrowsData(() => BackupEngine.CreateSnapshot(outside, Path.Combine(link, "new-library")))) throw new Exception("Linked destination ancestor was followed.");
    if (Directory.Exists(Path.Combine(outside, "new-library"))) throw new Exception("Linked output was written.");
    var linkedTarget = Path.Combine(link, "restore");
    if (!ThrowsData(() => BackupEngine.RestoreSnapshot(snapshotRoot, linkedTarget))) throw new Exception("Linked restore ancestor was followed.");
    Directory.Delete(link);
    var snapshotLink = Path.Combine(root, "snapshot-link"); Directory.CreateSymbolicLink(snapshotLink, snapshotRoot);
    if (BackupEngine.VerifySnapshot(snapshotLink)) throw new Exception("Linked snapshot root verified.");
    Directory.Delete(snapshotLink);
}
File.AppendAllText(Path.Combine(snapshotRoot, "notes.txt"), "changed"); if (BackupEngine.VerifySnapshot(snapshotRoot)) throw new Exception("Tampered snapshot verified incorrectly."); Directory.Delete(root, true); Console.WriteLine("PASS: atomic snapshot, restore preflight, traversal defense, collision safety, and tamper detection");
static bool Throws(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
static bool ThrowsIo(Action action) { try { action(); return false; } catch (IOException) { return true; } }
static bool ThrowsData(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }
