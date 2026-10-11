using Backstop;
var root = Path.Combine(Path.GetTempPath(), "openbackup-" + Guid.NewGuid().ToString("N")); var source = Path.Combine(root, "source"); var destination = Path.Combine(root, "snapshots"); Directory.CreateDirectory(source); File.WriteAllText(Path.Combine(source, "notes.txt"), "important");
var snapshot = BackupEngine.CreateSnapshot(source, destination); if (snapshot.Files.Count != 1 || !BackupEngine.VerifySnapshot(Path.Combine(destination, snapshot.Id))) throw new Exception("Fresh snapshot did not verify.");
if (!Throws(() => BackupEngine.CreateSnapshot(source, Path.Combine(source, "nested")))) throw new Exception("Nested destination was not rejected.");
var snapshotRoot = Path.Combine(destination, snapshot.Id);
var plan = BackupEngine.PlanRestore(snapshotRoot, Path.Combine(root, "planned")); if (plan.Files.Count != 1 || plan.Files[0].RelativePath != "notes.txt") throw new Exception("Restore preflight plan is incorrect.");
var restore = Path.Combine(root, "restore"); if (BackupEngine.RestoreSnapshot(snapshotRoot, restore) != 1 || File.ReadAllText(Path.Combine(restore, "notes.txt")) != "important") throw new Exception("Restore did not recreate the original file.");
var collisionTarget = Path.Combine(root, "collision"); Directory.CreateDirectory(collisionTarget); File.WriteAllText(Path.Combine(collisionTarget, "notes.txt"), "keep me");
if (!ThrowsIo(() => BackupEngine.RestoreSnapshot(snapshotRoot, collisionTarget)) || File.ReadAllText(Path.Combine(collisionTarget, "notes.txt")) != "keep me") throw new Exception("Restore collision preflight modified an existing file.");
var manifestPath = Path.Combine(snapshotRoot, "manifest.json"); var originalManifest = File.ReadAllText(manifestPath);
File.WriteAllText(manifestPath, originalManifest.Replace("notes.txt", "..\\\\escape.txt"));
if (!ThrowsData(() => BackupEngine.PlanRestore(snapshotRoot, Path.Combine(root, "unsafe")))) throw new Exception("Manifest traversal was not rejected.");
File.WriteAllText(manifestPath, originalManifest);
File.AppendAllText(Path.Combine(snapshotRoot, "notes.txt"), "changed"); if (BackupEngine.VerifySnapshot(snapshotRoot)) throw new Exception("Tampered snapshot verified incorrectly."); Directory.Delete(root, true); Console.WriteLine("PASS: atomic snapshot, restore preflight, traversal defense, collision safety, and tamper detection");
static bool Throws(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
static bool ThrowsIo(Action action) { try { action(); return false; } catch (IOException) { return true; } }
static bool ThrowsData(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }
