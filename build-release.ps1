<#
.SYNOPSIS
    Builds a distributable Phone Debug package.

.DESCRIPTION
    Cleans, tests, publishes both executables and lays out a folder that can be
    zipped and handed to anyone:

        dist\PhoneDebug-v0.1.0-win-x64\
            PhoneDebug.exe      the Windows app
            phone-debug.exe     the command line
            tools\              optional place for adb.exe / scrcpy.exe
            install.ps1
            uninstall.ps1
            README.md
            LICENSE
            CHANGELOG.md

.EXAMPLE
    .\build-release.ps1

.EXAMPLE
    .\build-release.ps1 -FrameworkDependent   # tiny package, needs the .NET 9 Desktop Runtime
#>
param(
    [string]$Runtime = "win-x64",
    [switch]$FrameworkDependent,
    [switch]$SkipTests,
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Write-Step($text) { Write-Host "`n$text" -ForegroundColor Cyan }
function Write-Ok($text) { Write-Host "  $text" -ForegroundColor Green }
function Write-Detail($text) { Write-Host "  $text" -ForegroundColor DarkGray }

function Invoke-Dotnet {
    param([string[]]$Arguments, [string]$FailureMessage)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n$FailureMessage" -ForegroundColor Red
        exit 1
    }
}

# ---------------------------------------------------------------- version

$propsPath = Join-Path $root "Directory.Build.props"
$props = Get-Content $propsPath -Raw
if ($props -notmatch "<Version>([^<]+)</Version>") {
    Write-Host "Could not read <Version> from Directory.Build.props" -ForegroundColor Red
    exit 1
}
$version = $Matches[1].Trim()

$packageName = "PhoneDebug-v$version-$Runtime"
$distRoot = Join-Path $root "dist"
$dist = Join-Path $distRoot $packageName

Write-Host ""
Write-Host "Phone Debug $version  ($Runtime)" -ForegroundColor White
Write-Host "----------------------------------------"

# ---------------------------------------------------------------- 1. clean

Write-Step "1/6  Cleaning"
foreach ($project in @("PhoneDebug.Core", "PhoneDebug.Cli", "PhoneDebug.App", "PhoneDebug.Tests")) {
    foreach ($folder in @("bin", "obj")) {
        $path = Join-Path $root "$project\$folder"
        if (-not (Test-Path $path)) { continue }

        try {
            Remove-Item -Recurse -Force $path -ErrorAction Stop
        }
        catch {
            # Usually a debug build still running. The package itself is built
            # fresh into dist, so this is a warning rather than a failure.
            Write-Warning "Could not clean $project\$folder (something is using it)."
        }
    }
}
if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
Write-Ok "old build output removed"

# ---------------------------------------------------------------- 2. tests

if ($SkipTests) {
    Write-Step "2/6  Tests (skipped)"
}
else {
    Write-Step "2/6  Running tests"
    Invoke-Dotnet @("test", (Join-Path $root "PhoneDebug.Tests\PhoneDebug.Tests.csproj"),
        "-c", "Release", "--nologo", "-v", "q") "Tests failed - the package was not built."
    Write-Ok "tests passed"
}

# ---------------------------------------------------------------- 3+4. publish

$publishArgs = @(
    "-c", "Release",
    "-r", $Runtime,
    "-o", $dist,
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "--nologo", "-v", "q"
)

if ($FrameworkDependent) {
    $publishArgs += @("--self-contained", "false")
}
else {
    $publishArgs += @(
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:EnableCompressionInSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true"
    )
}

Write-Step "3/6  Publishing the command line (phone-debug.exe)"
Invoke-Dotnet (@("publish", (Join-Path $root "PhoneDebug.Cli\PhoneDebug.Cli.csproj")) + $publishArgs) "Publishing the CLI failed."
Write-Ok "phone-debug.exe"

Write-Step "4/6  Publishing the Windows app (PhoneDebug.exe)"
Invoke-Dotnet (@("publish", (Join-Path $root "PhoneDebug.App\PhoneDebug.App.csproj")) + $publishArgs) "Publishing the app failed."
Write-Ok "PhoneDebug.exe"

# ---------------------------------------------------------------- 5. package

Write-Step "5/6  Assembling the package"

# Nothing but the runnable files belongs in a release.
Get-ChildItem $dist -File |
    Where-Object { $_.Extension -in @(".pdb", ".xml") } |
    Remove-Item -Force

foreach ($file in @("install.ps1", "uninstall.ps1", "README.md", "LICENSE", "CHANGELOG.md", "THIRD-PARTY-NOTICES.md")) {
    $source = Join-Path $root $file
    if (Test-Path $source) {
        Copy-Item $source (Join-Path $dist $file) -Force
    }
    else {
        Write-Host "  missing: $file" -ForegroundColor Yellow
    }
}

$toolsDir = Join-Path $dist "tools"
New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
Copy-Item (Join-Path $root "tools\README.md") (Join-Path $toolsDir "README.md") -Force

foreach ($exe in @("phone-debug.exe", "PhoneDebug.exe")) {
    if (-not (Test-Path (Join-Path $dist $exe))) {
        Write-Host "`n$exe is missing from the package." -ForegroundColor Red
        exit 1
    }
}
Write-Ok "package laid out"

# ---------------------------------------------------------------- 6. zip

$zipPath = Join-Path $distRoot "$packageName.zip"
if ($NoZip) {
    Write-Step "6/6  Zip (skipped)"
}
else {
    Write-Step "6/6  Creating the zip"
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    Compress-Archive -Path (Join-Path $dist "*") -DestinationPath $zipPath
    Write-Ok ("{0}  ({1:N1} MB)" -f (Split-Path $zipPath -Leaf), ((Get-Item $zipPath).Length / 1MB))
}

# ---------------------------------------------------------------- summary

$size = (Get-ChildItem $dist -Recurse -File | Measure-Object Length -Sum).Sum / 1MB

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host ""
Write-Detail ("Package : {0}" -f $dist)
Write-Detail ("Size    : {0:N1} MB" -f $size)
if (-not $NoZip) { Write-Detail ("Zip     : {0}" -f $zipPath) }
if ($FrameworkDependent) { Write-Detail "Requires: .NET 9 Desktop Runtime on the target machine" }
Write-Host ""
Write-Host "Install it locally with:" -ForegroundColor White
Write-Host "  powershell -ExecutionPolicy Bypass -File `"$dist\install.ps1`""
Write-Host ""
