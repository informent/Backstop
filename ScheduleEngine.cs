namespace Backstop;

public sealed record BackupSchedule(bool Enabled, TimeSpan Interval, DateTime StartLocal);

public static class ScheduleEngine
{
    public static DateTime? NextRun(BackupSchedule schedule, DateTime nowLocal)
    {
        if (!schedule.Enabled || schedule.Interval <= TimeSpan.Zero) return null;
        var next = schedule.StartLocal;
        while (next <= nowLocal) next = next.Add(schedule.Interval);
        return next;
    }
}
