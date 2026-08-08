param(
    [switch]$SelfContained
)

# Installs the "phone-debug" command so it can be run from any directory.
# Usage:  powershell -ExecutionPolicy Bypass -File scripts\install.ps1
# Optional: -SelfContained to ship the .NET runtime inside the exe.

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Bin = Join-Path $env:LOCALAPPDATA "PhoneDebug\bin"

New-Item -ItemType Directory -Force -Path $Bin | Out-Null

Write-Host "Publishing phone-debug -> $Bin" -ForegroundColor Cyan

$publishArgs = @(
    (Join-Path $Root "PhoneDebug.csproj"),
    "-c", "Release",
    "-o", $Bin,
    "-p:PublishSingleFile=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

if ($SelfContained) {
    $publishArgs += @("-r", "win-x64", "--self-contained", "true")
}

dotnet publish @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed." -ForegroundColor Red
    exit 1
}

# Add the bin folder to the user PATH so "phone-debug" works everywhere.
$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if (-not $userPath) { $userPath = "" }
if ($userPath -notlike "*$Bin*") {
    $joined = if ($userPath) { "$userPath;$Bin" } else { $Bin }
    [Environment]::SetEnvironmentVariable("PATH", $joined, "User")
    Write-Host "Added $Bin to the user PATH." -ForegroundColor Green
}

$exe = Join-Path $Bin "phone-debug.exe"
if (Test-Path $exe) {
    Write-Host ""
    Write-Host "Installed: $exe" -ForegroundColor Green
    Write-Host "Open a NEW terminal and run:  phone-debug" -ForegroundColor Green
} else {
    Write-Host "phone-debug.exe not found after publish." -ForegroundColor Red
    exit 1
}