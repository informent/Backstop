using System.IO;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace Backstop;

public partial class MainWindow : Window
{
    private string? source;
    private string? destination;
    private BackupSnapshot? selected;
    private BackupProfile profile = ProfileStore.Load();
    private readonly System.Windows.Controls.ComboBox retentionBox = new();
    private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenBackup", "schedule.json");

    public MainWindow()
    {
        InitializeComponent();
        source = profile.Source; destination = profile.Destination; if (source is not null || destination is not null) UpdateProfile();
        ScheduleInterval.SelectedIndex = 2;
        LoadSchedule();
        var schedulePanel = ScheduleInterval.Parent as System.Windows.Controls.Panel;
        if (schedulePanel is not null) { var retentionLabel = new TextBlock { Text = "Keep", Margin = new Thickness(12, 11, 4, 0), VerticalAlignment = VerticalAlignment.Center }; retentionBox.Width = 86; retentionBox.ItemsSource = new[] { 3, 7, 10, 30 }; retentionBox.SelectedItem = profile.RetentionCount is 3 or 7 or 10 or 30 ? profile.RetentionCount : 10; retentionBox.SelectionChanged += RetentionChanged; var taskButton = new System.Windows.Controls.Button { Content = "Install task", Margin = new Thickness(8, 8, 0, 0), Padding = new Thickness(10, 6, 10, 6) }; taskButton.Click += InstallTask_Click; var logButton = new System.Windows.Controls.Button { Content = "View log", Margin = new Thickness(8, 8, 0, 0), Padding = new Thickness(10, 6, 10, 6) }; logButton.Click += ViewLog_Click; schedulePanel.Children.Add(retentionLabel); schedulePanel.Children.Add(retentionBox); schedulePanel.Children.Add(taskButton); schedulePanel.Children.Add(logButton); }
    }

    private void ChooseSource_Click(object sender, RoutedEventArgs e) { source = ChooseFolder("Choose the folder to back up"); if (source is not null) { UpdateProfile(); SaveProfile(); } }
    private void ChooseDestination_Click(object sender, RoutedEventArgs e) { destination = ChooseFolder("Choose where snapshots should be stored"); if (destination is not null) { UpdateProfile(); SaveProfile(); } }
    private string? ChooseFolder(string description) { using var dialog = new Forms.FolderBrowserDialog { Description = description }; return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.SelectedPath : null; }
    private void UpdateProfile() { ProfileName.Text = source is null || destination is null ? "Profile in progress" : new DirectoryInfo(source).Name; ProfilePath.Text = $"{source ?? "Source not selected"}  â†’  {destination ?? "Destination not selected"}"; }

    private void CreateSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (source is null || destination is null) { StatusText.Text = "Choose source and destination first"; return; }
        try { StatusText.Text = "Creating snapshotâ€¦"; selected = BackupEngine.CreateSnapshot(source, destination); ActivityLog.Write($"Manual snapshot completed: {selected.Id}"); StatusText.Text = "Snapshot verified"; LatestText.Text = selected.Id; FileCountText.Text = selected.Files.Count.ToString("N0"); VerificationText.Text = $"{selected.Files.Count:N0} files copied with SHA-256 manifest."; RefreshSnapshots(); } catch (Exception ex) { ActivityLog.Write($"Manual snapshot failed: {ex.Message}"); StatusText.Text = "Backup failed"; VerificationText.Text = ex.Message; }
    }

    private void RefreshSnapshots() { if (destination is null) return; SnapshotList.ItemsSource = Directory.EnumerateDirectories(destination).OrderByDescending(x => x).Select(Path.GetFileName).ToArray(); }
    private void SnapshotList_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (destination is null || SnapshotList.SelectedItem is not string id) return; try { selected = BackupEngine.PreviewRestore(Path.Combine(destination, id)); SelectedText.Text = id; VerificationText.Text = $"{selected.Files.Count:N0} files Â· source {selected.Source}\nRestore preview only; no files will be changed."; } catch (Exception ex) { VerificationText.Text = ex.Message; } }
    private void VerifySnapshot_Click(object sender, RoutedEventArgs e) { if (destination is null || selected is null) { VerificationText.Text = "Select a snapshot first."; return; } var ok = BackupEngine.VerifySnapshot(Path.Combine(destination, selected.Id)); ActivityLog.Write($"Verification {(ok ? "passed" : "failed")}: {selected.Id}"); VerificationText.Text = ok ? "Integrity verified. Every manifest entry matches." : "Verification failed. One or more files differ."; StatusText.Text = ok ? "Verified" : "Integrity issue"; }
    private void RestoreSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (destination is null || selected is null) { VerificationText.Text = "Select a snapshot first."; return; }
        var target = ChooseFolder("Choose an empty folder for the restored files");
        if (target is null) return;
        try { var count = BackupEngine.RestoreSnapshot(Path.Combine(destination, selected.Id), target); ActivityLog.Write($"Restore completed: {selected.Id} ({count} files)"); VerificationText.Text = $"Restored {count:N0} files to {target}. Existing files were never overwritten."; StatusText.Text = "Restore complete"; } catch (Exception ex) { ActivityLog.Write($"Restore failed: {ex.Message}"); VerificationText.Text = ex.Message; StatusText.Text = "Restore stopped"; }
    }

    private void ScheduleChanged(object sender, RoutedEventArgs e)
    {
        if (ScheduleInterval.SelectedItem is not ComboBoxItem item || item.Tag is not string hoursText || !double.TryParse(hoursText, out var hours)) return;
        var schedule = new BackupSchedule(ScheduleEnabled.IsChecked == true, TimeSpan.FromHours(hours), DateTime.Now);
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, System.Text.Json.JsonSerializer.Serialize(schedule)); profile = profile with { Source = source, Destination = destination, Schedule = schedule }; ProfileStore.Save(profile);
        UpdateNextRun(schedule);
    }

    private void SaveProfile() => ProfileStore.Save(profile with { Source = source, Destination = destination });
    private void RetentionChanged(object sender, SelectionChangedEventArgs e) { if (retentionBox.SelectedItem is int count) { profile = profile with { RetentionCount = count }; ProfileStore.Save(profile with { Source = source, Destination = destination }); } }
    private void ViewLog_Click(object sender, RoutedEventArgs e) { var text = File.Exists(ActivityLog.PathOnDisk) ? File.ReadAllText(ActivityLog.PathOnDisk) : "No activity has been recorded yet."; System.Windows.MessageBox.Show(text, "Backstop activity log", MessageBoxButton.OK, MessageBoxImage.Information); }
    private void InstallTask_Click(object sender, RoutedEventArgs e)
    {
        var hours = ScheduleInterval.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var parsed) ? parsed : 24;
        var ok = TaskSchedulerService.Install(Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "Backstop.exe"), hours);
        VerificationText.Text = ok ? "Windows Task Scheduler task installed. Backstop can now run while closed." : "Windows Task Scheduler could not create the task.";
        StatusText.Text = ok ? "Task installed" : "Task setup failed";
    }

    private void LoadSchedule()
    {
        try { if (File.Exists(SettingsPath)) { var schedule = System.Text.Json.JsonSerializer.Deserialize<BackupSchedule>(File.ReadAllText(SettingsPath)); if (schedule is not null) { ScheduleEnabled.IsChecked = schedule.Enabled; ScheduleInterval.SelectedIndex = schedule.Interval.TotalHours switch { 1 => 0, 6 => 1, 24 => 2, 168 => 3, _ => 2 }; UpdateNextRun(schedule); return; } } } catch { }
        ScheduleEnabled.IsChecked = false;
        UpdateNextRun(new BackupSchedule(false, TimeSpan.FromDays(1), DateTime.Now));
    }

    private void UpdateNextRun(BackupSchedule schedule)
    {
        var next = ScheduleEngine.NextRun(schedule, DateTime.Now);
        NextRunText.Text = next is null ? "Not scheduled" : $"Next snapshot {next.Value:ddd, MMM d Â· h:mm tt}";
    }
}
