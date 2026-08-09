<#
.SYNOPSIS
    Installs Phone Debug for the current user.

.DESCRIPTION
    Run this from a release folder (next to PhoneDebug.exe and phone-debug.exe)
    or from a clone of the repository - it detects which and does the right thing.

    It copies the program to %LOCALAPPDATA%\PhoneDebug\bin, puts that folder on
    your PATH so "phone-debug" works from anywhere, adds a Start Menu shortcut,
    and checks that adb and scrcpy are available.

    Nothing is installed system-wide and no administrator rights are needed.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File install.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File install.ps1 -NoShortcut
#>
param(
    [switch]$NoShortcut,
    [switch]$NoPath,
    [switch]$SkipDependencyCheck,
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "PhoneDebug"
$binDir = Join-Path $installDir "bin"
$source = $PSScriptRoot

function Write-Title($text) { Write-Host "`n$text" -ForegroundColor Cyan }
function Write-Ok($text) { Write-Host "  $([char]0x2713) $text" -ForegroundColor Green }
function Write-Warn($text) { Write-Host "  ! $text" -ForegroundColor Yellow }
function Write-Fail($text) { Write-Host "  x $text" -ForegroundColor Red }

function Test-Tool([string]$Name) {
    if (Get-Command $Name -ErrorAction SilentlyContinue) { return $true }
    return (Test-Path (Join-Path $binDir "tools\$Name.exe"))
}

Write-Host ""
Write-Host "Phone Debug - install" -ForegroundColor White
Write-Host "----------------------------------------"

# ---------------------------------------------------------------- 1. source

$hasBinaries = (Test-Path (Join-Path $source "phone-debug.exe")) -and (Test-Path (Join-Path $source "PhoneDebug.exe"))
$hasSources = Test-Path (Join-Path $source "PhoneDebug.sln")

if (-not $hasBinaries -and $hasSources) {
    Write-Title "Building from source"

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Fail "The .NET SDK is required to build from source: https://dotnet.microsoft.com/download"
        exit 1
    }

    & (Join-Path $source "build-release.ps1") -NoZip
    if ($LASTEXITCODE -ne 0) { exit 1 }

    $built = Get-ChildItem (Join-Path $source "dist") -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $built) {
        Write-Fail "The build did not produce a package."
        exit 1
    }

    $source = $built.FullName
    Write-Ok "built $($built.Name)"
}
elseif (-not $hasBinaries) {
    Write-Fail "PhoneDebug.exe and phone-debug.exe were not found next to this script."
    Write-Host "    Run install.ps1 from an unpacked release folder, or from the project source." -ForegroundColor DarkGray
    exit 1
}

# ---------------------------------------------------------------- 2. copy

Write-Title "Installing to $binDir"

if ((Resolve-Path $source).Path -eq (Resolve-Path -Path $binDir -ErrorAction SilentlyContinue).Path) {
    Write-Ok "already in place"
}
else {
    $running = Get-Process -Name "PhoneDebug", "phone-debug" -ErrorAction SilentlyContinue
    if ($running) {
        Write-Warn "closing the running Phone Debug"
        $running | Stop-Process -Force
        Start-Sleep -Milliseconds 500
    }

    New-Item -ItemType Directory -Force -Path $binDir | Out-Null

    foreach ($item in @("PhoneDebug.exe", "phone-debug.exe", "README.md", "LICENSE", "CHANGELOG.md", "THIRD-PARTY-NOTICES.md", "uninstall.ps1", "install.ps1")) {
        $path = Join-Path $source $item
        if (Test-Path $path) { Copy-Item $path (Join-Path $binDir $item) -Force }
    }

    # Framework-dependent packages also ship their .dll and .json files.
    Get-ChildItem $source -File | Where-Object { $_.Extension -in @(".dll", ".json") } | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $binDir $_.Name) -Force
    }

    $toolsSource = Join-Path $source "tools"
    if (Test-Path $toolsSource) {
        Copy-Item $toolsSource $binDir -Recurse -Force
    }
    else {
        New-Item -ItemType Directory -Force -Path (Join-Path $binDir "tools") | Out-Null
    }

    Write-Ok "files copied"
}

# ---------------------------------------------------------------- 3. PATH

if ($NoPath) {
    Write-Title "PATH (skipped)"
}
else {
    Write-Title "Adding phone-debug to your PATH"

    $userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if (-not $userPath) { $userPath = "" }

    $entries = $userPath -split ";" | Where-Object { $_ -ne "" }
    if ($entries -notcontains $binDir) {
        $joined = (@($entries) + $binDir) -join ";"
        [Environment]::SetEnvironmentVariable("PATH", $joined, "User")
        Write-Ok "added $binDir"
    }
    else {
        Write-Ok "already on your PATH"
    }

    # Makes it work in this window too, not only in new ones.
    if (($env:PATH -split ";") -notcontains $binDir) {
        $env:PATH = "$env:PATH;$binDir"
    }
}

# ---------------------------------------------------------------- 4. shortcut

if ($NoShortcut) {
    Write-Title "Start Menu shortcut (skipped)"
}
else {
    Write-Title "Adding a Start Menu shortcut"
    try {
        $startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
        $shortcut = Join-Path $startMenu "Phone Debug.lnk"

        $shell = New-Object -ComObject WScript.Shell
        $link = $shell.CreateShortcut($shortcut)
        $link.TargetPath = Join-Path $binDir "PhoneDebug.exe"
        $link.WorkingDirectory = $binDir
        $link.Description = "Phone Debug"
        $link.Save()

        Write-Ok "Start Menu > Phone Debug"
    }
    catch {
        Write-Warn "could not create the shortcut: $($_.Exception.Message)"
    }
}

# ---------------------------------------------------------------- 5. tools

if (-not $SkipDependencyCheck) {
    Write-Title "Checking adb and scrcpy"

    $winget = Get-Command winget -ErrorAction SilentlyContinue

    foreach ($tool in @(
            @{ Name = "adb"; Package = "Google.PlatformTools"; Label = "ADB (Android platform-tools)" },
            @{ Name = "scrcpy"; Package = "Genymobile.scrcpy"; Label = "scrcpy" })) {

        if (Test-Tool $tool.Name) {
            Write-Ok "$($tool.Label) found"
            continue
        }

        if ($winget) {
            Write-Host "  installing $($tool.Label) with winget..." -ForegroundColor DarkGray
            winget install --id $tool.Package --silent --accept-package-agreements --accept-source-agreements --disable-interactivity | Out-Host

            if (Test-Tool $tool.Name) {
                Write-Ok "$($tool.Label) installed"
            }
            else {
                Write-Warn "$($tool.Label) still not on the PATH - reopen your terminal and check again"
            }
        }
        else {
            Write-Warn "$($tool.Label) is missing and winget is not available."
            Write-Host "    Install it manually, then rerun this script:" -ForegroundColor DarkGray
            Write-Host "      winget install $($tool.Package)" -ForegroundColor DarkGray
            Write-Host "    Or copy $($tool.Name).exe into: $binDir\tools" -ForegroundColor DarkGray
        }
    }
}

# ---------------------------------------------------------------- done

Write-Host ""
Write-Host "Installed." -ForegroundColor Green
Write-Host ""

if ($NoLaunch) {
    Write-Host "  The app is in your Start Menu: 'Phone Debug'" -ForegroundColor White
    Write-Host "  Or run  phone-debug  in a terminal (CLI)." -ForegroundColor White
}
else {
    $app = Join-Path $binDir "PhoneDebug.exe"
    if (Test-Path $app) {
        Write-Host "  Starting Phone Debug..." -ForegroundColor White
        Start-Process $app
    }
    else {
        Write-Host "  Start 'Phone Debug' from the Start Menu." -ForegroundColor White
    }
}

Write-Host ""
Write-Host "  Uninstall with:  powershell -ExecutionPolicy Bypass -File `"$binDir\uninstall.ps1`"" -ForegroundColor DarkGray
Write-Host ""
