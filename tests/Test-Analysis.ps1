param([Parameter(Mandatory=$true)][string]$Exe)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('backstop-packaged-analysis-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
try {
    foreach ($id in @('before','after')) {
        $folder = Join-Path $root $id
        New-Item -ItemType Directory -Path $folder | Out-Null
        $file = Join-Path $folder 'notes.txt'
        [IO.File]::WriteAllText($file, $id)
        $manifest = @{ Id=$id; CreatedUtc=[DateTime]::UtcNow.ToString('o'); Source=$root; Destination=$root; Files=@(@{RelativePath='notes.txt';Length=(Get-Item $file).Length;Sha256=(Get-FileHash $file).Hash}) }
        [IO.File]::WriteAllText((Join-Path $folder 'manifest.json'), ($manifest | ConvertTo-Json -Depth 6))
    }
    $report = Join-Path $root 'comparison.json'
    $arguments = '--compare "' + (Join-Path $root 'before') + '" "' + (Join-Path $root 'after') + '" --report "' + $report + '"'
    $process = Start-Process -FilePath $Exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(30000)) { $process.Kill(); throw 'Analysis command timed out' }
    if ($process.ExitCode -ne 0) { throw 'Comparison command failed' }
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($result.Files[0].Kind -ne 'Modified') { throw 'Comparison report is incorrect' }
    $digest = (Get-FileHash $report).Hash
    $process = Start-Process -FilePath $Exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(30000)) { $process.Kill(); throw 'Analysis command timed out' }
    if ($process.ExitCode -ne 2 -or (Get-FileHash $report).Hash -ne $digest) { throw 'Existing report was not preserved' }
    $target = Join-Path $root 'restore-target'
    $report = Join-Path $root 'restore.json'
    $arguments = '--preview-restore "' + (Join-Path $root 'after') + '" "' + $target + '" --report "' + $report + '"'
    $process = Start-Process -FilePath $Exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(30000)) { $process.Kill(); throw 'Analysis command timed out' }
    if ($process.ExitCode -ne 0 -or (Test-Path -LiteralPath $target)) { throw 'Restore preview failed or modified destination' }
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if (!$result.CanRestoreWithoutOverwrite -or $result.Files[0].Kind -ne 'NewFile') { throw 'Restore preview report is incorrect' }
    Write-Output 'PASS: packaged comparison, safe report creation and read-only restore preview'
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or !([IO.Path]::GetFileName($resolved)).StartsWith('backstop-packaged-analysis-')) { throw 'Unsafe cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
