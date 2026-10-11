param([string]$InstallRoot = "$env:LOCALAPPDATA\Backstop")
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'Backstop.exe'
if (!(Test-Path -LiteralPath $source)) { throw 'Backstop.exe must be beside Install.ps1.' }
New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $InstallRoot 'Backstop.exe') -Force
$shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Backstop.lnk'
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut); $link.TargetPath = Join-Path $InstallRoot 'Backstop.exe'; $link.WorkingDirectory = $InstallRoot; $link.Description = 'Backstop local backup manager'; $link.Save()
Write-Output "Installed Backstop to $InstallRoot"
