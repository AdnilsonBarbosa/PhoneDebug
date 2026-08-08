<#
  phone-debug  -  distribuição web (uma linha)
  -------------------------------------------------
  ANTES DE PUBLICAR: substitui <SEU-UTILIZADOR> abaixo pelo teu utilizador GitHub,
  ou passa '-Repo <utilizador>/phone-debug' quando correres o script diretamente.

  Instalação:
      irm https://github.com/<utilizador>/phone-debug/releases/latest/download/install.ps1 | iex

  Desinstalar:
      Volta a descarregar este script e corre:
      & "$env:TEMP\phone-debug-install.ps1" -Uninstall
#>
param(
    [switch]$Uninstall,
    [string]$Repo = "AdnilsonBarbosa/PhoneDebug"
)

$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "PhoneDebug"
$binDir     = Join-Path $installDir "bin"
$exe        = Join-Path $binDir "phone-debug.exe"

function Write-Info($m)  { Write-Host $m -ForegroundColor Cyan }
function Write-Fail($m)  { Write-Host $m -ForegroundColor Red }

function Get-UserPath {
    $p = [Environment]::GetEnvironmentVariable("PATH", "User")
    if (-not $p) { $p = "" }
    return $p
}

function Add-ToUserPath([string]$Dir) {
    $current = Get-UserPath
    if (($current -split ";") -notcontains $Dir) {
        $joined = if ($current) { "$current;$Dir" } else { $Dir }
        [Environment]::SetEnvironmentVariable("PATH", $joined, "User")
        Write-Info "  Adicionado ao PATH: $Dir"
    }
}

function Remove-FromUserPath([string]$Dir) {
    $current = Get-UserPath
    $novo = (($current -split ";") | Where-Object { $_ -ne "" -and $_ -ne $Dir }) -join ";"
    [Environment]::SetEnvironmentVariable("PATH", $novo, "User")
}

function Test-Command([string]$Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

Write-Info "================================="
Write-Info "  Phone Debug Terminal"
Write-Info "================================="
Write-Info ""

if ($Uninstall) {
    Write-Info "A remover..."
    if (Test-Path $installDir) {
        Remove-Item -Recurse -Force $installDir
    }
    Remove-FromUserPath $binDir
    Write-Info "phone-debug removido. Reabre o terminal."
    exit 0
}

# 1. Download da release.
Write-Host "A descarregar phone-debug do GitHub..."
$url = "https://github.com/$Repo/releases/latest/download/phone-debug.zip"
$zip = Join-Path $env:TEMP "phone-debug-download.zip"

try {
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
}
catch {
    Write-Fail "Falha na transferência. Confirma que o repositório '$Repo' tem uma release com 'phone-debug.zip'."
    exit 1
}

# 2. Extrair.
New-Item -ItemType Directory -Force -Path $binDir | Out-Null
Expand-Archive -Path $zip -DestinationPath $binDir -Force
Remove-Item -Force $zip

if (-not (Test-Path $exe)) {
    Write-Fail "phone-debug.exe nao encontrado no pacote descarregado."
    exit 1
}

Write-Info "  OK -> $exe"

# 3. PATH do utilizador.
Add-ToUserPath $binDir

# 4. Dependências: adb + scrcpy.
$winget = Get-Command winget -ErrorAction SilentlyContinue

if (-not (Test-Command "adb")) {
    Write-Info "adb nao encontrado. A instalar..."
    if ($winget) {
        winget install --id Google.PlatformTools --silent --accept-package-agreements --accept-source-agreements --disable-interactivity | Out-Host
    }
    if (-not (Test-Command "adb")) {
        Write-Info "  winget indisponivel; a descarregar platform-tools diretamente..."
        $ptZip = Join-Path $env:TEMP "platform-tools.zip"
        try {
            Invoke-WebRequest -Uri "https://dl.google.com/android/repository/platform-tools-latest-windows.zip" -OutFile $ptZip -UseBasicParsing
            $ptDir = Join-Path $installDir "platform-tools"
            New-Item -ItemType Directory -Force -Path $ptDir | Out-Null
            Expand-Archive -Path $ptZip -DestinationPath $ptDir -Force
            Remove-Item -Force $ptZip
            Add-ToUserPath (Join-Path $ptDir "platform-tools")
        }
        catch {
            Write-Host "  AVISO: falhou instalar adb. Instala https://dl.google.com/android/repository/platform-tools-latest-windows.zip" -ForegroundColor Yellow
        }
    }
}

if (-not (Test-Command "scrcpy")) {
    Write-Info "scrcpy nao encontrado. A instalar..."
    if ($winget) {
        winget install --id Genymobile.scrcpy --silent --accept-package-agreements --accept-source-agreements --disable-interactivity | Out-Host
    }
    if (-not (Test-Command "scrcpy")) {
        Write-Host "falha: instala scrcpy com: winget install Genymobile.scrcpy" -ForegroundColor Yellow
    }
}

# 5. Fim.
Write-Info ""
Write-Info "Instalado com sucesso!"
Write-Info "Reabre o terminal e corre:   phone-debug"
Write-Info "Outros comandos: phone-debug help"
Write-Info ""
Write-Info "Desinstalar: descarrega de novo este script e corre-o com -Uninstall"