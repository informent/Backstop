namespace Backstop;
public static class ScheduledRunner
{
    public static bool RunIfDue(BackupProfile profile, DateTime nowLocal, string? profilePath = null, string? activityLogPath = null)
    {
        if (!profile.Schedule.Enabled || string.IsNullOrWhiteSpace(profile.Source) || string.IsNullOrWhiteSpace(profile.Destination)) return false;
        if (profile.LastRunLocal is not null && nowLocal < profile.LastRunLocal.Value.Add(profile.Schedule.Interval)) return false;
        try { BackupEngine.CreateSnapshot(profile.Source, profile.Destination); RetentionEngine.RemoveOlderSnapshots(profile.Destination, profile.RetentionCount); ProfileStore.Save(profile with { LastRunLocal = nowLocal }, profilePath); ActivityLog.Write("Scheduled snapshot completed", activityLogPath); return true; } catch (Exception ex) { ActivityLog.Write($"Scheduled snapshot failed: {ex.Message}", activityLogPath); throw; }
    }
}
