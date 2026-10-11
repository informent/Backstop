param([string]$InstallRoot = "$env:LOCALAPPDATA\Backstop")
$ErrorActionPreference = 'Stop'
$shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Backstop.lnk'
if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force }
if (Test-Path -LiteralPath $InstallRoot) { Remove-Item -LiteralPath $InstallRoot -Recurse -Force }
Write-Output 'Backstop was removed. Existing backup snapshots were not touched.'
