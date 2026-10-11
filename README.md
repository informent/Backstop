# Backstop

Backstop is a local-first Windows backup application focused on one promise: a backup is not complete until it can be verified.

Backstop creates versioned snapshots with SHA-256 manifests, preflighted restores, persisted profiles, and schedule-aware unattended execution. Snapshot creation is atomic: incomplete work stays in a temporary folder and is removed after a failure. Restore preflight validates every hash, length, relative path, duplicate, and destination collision before the first file is copied. Use the dashboard's **Install task** action to register Windows Task Scheduler, or run the published executable with `--run-scheduled`. It never deletes source files or uploads data.

Version 1.5 hardens retention cleanup with an inspectable plan. Only direct, non-reparse snapshot folders whose manifest ID matches the folder are eligible; candidates are ordered by manifest creation time, sized before deletion, and revalidated immediately before removal. Decoy, malformed, and inaccessible folders are reported and preserved.

For a local install, place `Backstop.exe` beside `Install.ps1` and run `powershell -ExecutionPolicy Bypass -File .\Install.ps1`. The installer creates a desktop shortcut. `Uninstall.ps1` removes only the application and shortcut; it never removes backup snapshots.

MIT licensed.

## Name change

Backstop replaces OpenBackup. New builds use Backstop.exe and Backstop.csproj. Existing releases remain available under their original names.

Existing settings and data locations retain the OpenBackup folder name for compatibility. Keep these folders when upgrading; no automatic move or data format change is required.

The existing OpenBackup scheduled-task name is retained to replace the same task rather than create a second schedule. Run Install task after installing the new executable to update its path.
