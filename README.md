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

## Snapshot analysis

Compare two verified snapshots without changing them:

```powershell
Start-Process .\Backstop.exe -ArgumentList '--compare "D:\Snapshots\OLDER" "D:\Snapshots\NEWER" --report "D:\Reports\comparison.json"' -Wait -PassThru
```

The JSON report classifies each file as Added, Removed, Modified or Unchanged and reports the net byte change. Both snapshots must pass integrity checks.

Inspect a restore destination without writing into it:

```powershell
Start-Process .\Backstop.exe -ArgumentList '--preview-restore "D:\Snapshots\NEWER" "D:\Restore" --report "D:\Reports\restore.json"' -Wait -PassThru
```

The report classifies targets as NewFile, Identical, Conflict or Blocked. Existing files are never overwritten; filesystem links and directory/file collisions are blockers. The report folder must already exist, and the report filename must be new. Exit code 0 means a report was created; exit code 2 means invalid inputs, failed integrity checks or a report write failure. Analysis runs headlessly. A preview describes the current filesystem and must be refreshed if files change before restoring.

The comparison UI is still under development.

## Automatic setup

Windows setup packages install Backstop for your current account, bundle the .NET runtime, create a Start menu shortcut and register an uninstaller. A desktop shortcut is optional. No administrator password or manual file copying is required. Existing settings and saved data stay in their current locations. The portable ZIP remains available.

Download `Backstop-v1.7.0-setup.exe` from Releases and run it.

For unattended deployment:

```powershell
Start-Process .\Backstop-v1.7.0-setup.exe -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-' -Wait -PassThru
```

Build from source with the [open-source Inno Setup compiler](https://github.com/jrsoftware/issrc) installed:

```powershell
pwsh -File scripts/Build-Setup.ps1 -Verify
```

The verification option installs into a temporary test folder, checks reinstall and launch, then uninstalls. It runs on the hosted Windows runner during CI. It does not remove existing user settings or backups.

## Snapshot safety in 1.7

Capture and restore reject filesystem links in every existing ancestor, including linked source folders, destination libraries and snapshot manifests. Capture checks each directory before descending and records lengths from the copied bytes. Restore preflights directory collisions and files blocking parent folders before writing its first file, then repeats path checks during copying.

The root filename `manifest.json` is reserved for snapshot metadata. A source using that filename is rejected with an explanation and its incomplete snapshot is removed. Nested files named `manifest.json` are supported. Restore now stages and verifies every copied byte before moving files into place. If a promotion step fails, already moved files are rolled back when they still match the verified snapshot; unrelated destination files are preserved. This protects against ordinary operation failures, but a power loss during the short promotion phase can still require manual recovery. Keep source snapshots and restore destinations unchanged while a restore runs.
