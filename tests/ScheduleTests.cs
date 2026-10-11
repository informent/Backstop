using Backstop;
using System.Text.Json;

var start = new DateTime(2026, 1, 1, 8, 0, 0);
var next = ScheduleEngine.NextRun(new BackupSchedule(true, TimeSpan.FromHours(24), start), start.AddHours(25));
if (next != start.AddHours(48)) throw new Exception("Next-run calculation is incorrect.");
if (ScheduleEngine.NextRun(new BackupSchedule(false, TimeSpan.FromHours(1), start), start) is not null) throw new Exception("Disabled schedule returned a run.");
Console.WriteLine("PASS: schedule next-run calculation");

var root = Path.Combine(Path.GetTempPath(), "openbackup-scheduled-" + Guid.NewGuid().ToString("N"));
try
{
    var source = Path.Combine(root, "source");
    var destination = Path.Combine(root, "snapshots");
    var profilePath = Path.Combine(root, "profile.json");
    var activityLogPath = Path.Combine(root, "activity.log");
    Directory.CreateDirectory(source);
    File.WriteAllText(Path.Combine(source, "scheduled.txt"), "scheduled");
    var profile = new BackupProfile(source, destination, new BackupSchedule(true, TimeSpan.FromHours(1), DateTime.Now.AddHours(-2)), null, 1);

    if (!ScheduledRunner.RunIfDue(profile, DateTime.Now, profilePath, activityLogPath) || !Directory.Exists(destination)) throw new Exception("Scheduled runner did not create a snapshot.");
    if (!File.Exists(profilePath) || !File.Exists(activityLogPath)) throw new Exception("Scheduled runner did not persist its profile and activity log to the supplied paths.");
    var savedProfile = JsonSerializer.Deserialize<BackupProfile>(File.ReadAllText(profilePath));
    if (savedProfile?.LastRunLocal is null) throw new Exception("Scheduled runner did not save its last-run time.");
    if (!File.ReadAllText(activityLogPath).Contains("Scheduled snapshot completed", StringComparison.Ordinal)) throw new Exception("Scheduled activity log is missing its completion record.");
    if (ScheduledRunner.RunIfDue(profile with { LastRunLocal = DateTime.Now }, DateTime.Now, profilePath, activityLogPath)) throw new Exception("Scheduled runner ignored interval guard.");
    Console.WriteLine("PASS: unattended scheduled runner uses isolated persistence and respects interval guard");

    Thread.Sleep(1100);
    File.WriteAllText(Path.Combine(source, "second.txt"), "second");
    BackupEngine.CreateSnapshot(source, destination);
    var decoy = Path.Combine(destination, "not-a-snapshot");
    Directory.CreateDirectory(decoy);
    File.WriteAllText(Path.Combine(decoy, "keep.txt"), "do not delete");
    File.WriteAllText(Path.Combine(decoy, "manifest.json"), "{}");

    var retentionPlan = RetentionEngine.Plan(destination, 1);
    if (retentionPlan.Retained.Count != 1 || retentionPlan.Remove.Count != 1 || retentionPlan.Skipped.Count != 1) throw new Exception("Retention plan did not separate valid snapshots from decoys.");
    if (retentionPlan.Remove.Sum(x => x.Bytes) <= 0) throw new Exception("Retention plan did not calculate reclaimable bytes.");
    if (RetentionEngine.Apply(retentionPlan, destination) != 1 || !Directory.Exists(decoy) || File.ReadAllText(Path.Combine(decoy, "keep.txt")) != "do not delete") throw new Exception("Retention deleted an unvalidated folder.");
    try { RetentionEngine.Plan(destination, 0); throw new Exception("Unsafe zero-retention plan was accepted."); }
    catch (ArgumentOutOfRangeException) { }
    Console.WriteLine("PASS: validated retention planning keeps newest snapshot and protects decoy folders");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, true);
}
