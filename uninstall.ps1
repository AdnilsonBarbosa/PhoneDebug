<#
.SYNOPSIS
    Removes Phone Debug for the current user.

.DESCRIPTION
    Deletes %LOCALAPPDATA%\PhoneDebug\bin, takes it off your PATH and removes the
    Start Menu shortcut. adb and scrcpy are left alone - they were installed
    separately and other tools may rely on them.

    Logs and screenshots are kept unless you pass -RemoveData.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File uninstall.ps1
#>
param(
    [switch]$RemoveData
)

$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "PhoneDebug"
$binDir = Join-Path $installDir "bin"
$logDir = Join-Path $installDir "logs"

function Write-Title($text) { Write-Host "`n$text" -ForegroundColor Cyan }
function Write-Ok($text) { Write-Host "  $([char]0x2713) $text" -ForegroundColor Green }
function Write-Warn($text) { Write-Host "  ! $text" -ForegroundColor Yellow }

Write-Host ""
Write-Host "Phone Debug - uninstall" -ForegroundColor White
Write-Host "----------------------------------------"

# ---------------------------------------------------------------- 1. processes

Write-Title "Closing Phone Debug"
$running = Get-Process -Name "PhoneDebug", "phone-debug" -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
    Write-Ok "closed"
}
else {
    Write-Ok "not running"
}

# ---------------------------------------------------------------- 2. files

Write-Title "Removing files"

# The script may be running from inside the folder it is deleting.
$self = $PSScriptRoot
$deleteBin = $true

if (Test-Path $binDir) {
    try {
        Remove-Item -Recurse -Force $binDir
        Write-Ok "removed $binDir"
    }
    catch {
        $deleteBin = $false
        Write-Warn "could not remove $binDir : $($_.Exception.Message)"
        if ($self -and $self.StartsWith($binDir, [StringComparison]::OrdinalIgnoreCase)) {
            Write-Host "    Copy uninstall.ps1 elsewhere and run it again." -ForegroundColor DarkGray
        }
    }
}
else {
    Write-Ok "nothing installed at $binDir"
}

if ($RemoveData) {
    foreach ($folder in @($logDir)) {
        if (Test-Path $folder) {
            Remove-Item -Recurse -Force $folder
            Write-Ok "removed $folder"
        }
    }
}
elseif (Test-Path $logDir) {
    Write-Host "  logs kept in $logDir (use -RemoveData to delete them)" -ForegroundColor DarkGray
}

if ($deleteBin -and (Test-Path $installDir)) {
    $left = Get-ChildItem $installDir -Force -ErrorAction SilentlyContinue
    if (-not $left) {
        Remove-Item -Force $installDir
        Write-Ok "removed $installDir"
    }
}

# ---------------------------------------------------------------- 3. PATH

Write-Title "Cleaning your PATH"

$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if (-not $userPath) { $userPath = "" }

$entries = $userPath -split ";" | Where-Object { $_ -ne "" -and $_ -ne $binDir }
$joined = $entries -join ";"

if ($joined -ne $userPath) {
    [Environment]::SetEnvironmentVariable("PATH", $joined, "User")
    Write-Ok "removed $binDir"
}
else {
    Write-Ok "nothing to remove"
}

# ---------------------------------------------------------------- 4. shortcut

Write-Title "Removing the Start Menu shortcut"
$shortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Phone Debug.lnk"
if (Test-Path $shortcut) {
    Remove-Item -Force $shortcut
    Write-Ok "removed"
}
else {
    Write-Ok "none found"
}

# ---------------------------------------------------------------- done

Write-Host ""
Write-Host "Phone Debug was removed." -ForegroundColor Green
Write-Host "  adb and scrcpy were left installed." -ForegroundColor DarkGray
Write-Host "  Close this terminal so the PATH change takes effect." -ForegroundColor DarkGray
Write-Host ""
