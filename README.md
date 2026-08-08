# Phone Debug Terminal

Ferramenta de linha de comandos (CLI) para Windows para depurar um telemóvel
Android via USB. Executar `phone-debug` e ele trata do resto.

## Instalação (uma linha, para qualquer utilizador)

```powershell
irm https://github.com/<SEU-UTILIZADOR>/phone-debug/releases/latest/download/install.ps1 | iex
```

O instalador transfere o executável da **última release** do GitHub, põe
`phone-debug` no PATH e instala `adb` e `scrcpy` automaticamente (se faltarem).

Depois, num terminal **novo**:

```powershell
phone-debug
```

## Requisitos (para compilar / publicar)

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Android platform-tools](https://dl.google.com/android/repository/platform-tools-latest-windows.zip) -> `adb.exe` no PATH
- [scrcpy](https://github.com/Genymobile/scrcpy) -> `scrcpy` no PATH

Instalar o scrcpy:

```powershell
winget install Genymobile.scrcpy
# ou
scoop install scrcpy
# ou
choco install scrcpy --yes
```

Depois de instalar, **reabra** o terminal.

## Instalação (comando `phone-debug` em qualquer diretório)

```powershell
powershell -ExecutionPolicy Bypass -File scripts\install.ps1
```

Publica um executável standalone (`phone-debug.exe`) em
`%LOCALAPPDATA%\PhoneDebug\bin`, adiciona essa pasta ao PATH do utilizador e
fica disponível como `phone-debug`.

Para incluir o .NET runtime dentro do exe (sem necessidade de ter o .NET
instalado no alvo):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\install.ps1 -SelfContained
```

Abra um **novo** terminal e:

```powershell
phone-debug
```

## Comandos

| Comando | Descrição |
| --- | --- |
| `phone-debug` | vigia o telemóvel: espera o dispositivo, mostra modelo/Android/serial e abre o ecrã automaticamente (e novamente a cada nova ligação) |
| `phone-debug devices` | lista os dispositivos ligados |
| `phone-debug mirror` | abre o scrcpy diretamente |
| `phone-debug logs` | executa `adb logcat` (streaming, `Ctrl+C` para parar) |
| `phone-debug info` | detalhes do dispositivo (modelo, fabricante, Android, SDK, patch) |
| `phone-debug screenshot` | captura o ecrã para `screenshots/screenshot-<data-hora>.png` |
| `phone-debug install <apk>` | instala com `adb install -r <apk>` |
| `phone-debug reboot` | reinicia o dispositivo |

### Exemplo: `phone-debug` (sem argumentos)

```text
PHONE DEBUG

Checking ADB...
ADB: OK

Checking scrcpy...
scrcpy: OK

Waiting for Android device...

Device connected
  Model:   2312FPCA6G
  Android: 16
  Serial:  adb-dym7am4luca649yh-op4RB1._adb-tls-connect._tcp

Opening screen...
```

- Ao desligar o telemóvel, volta ao estado de espera.
- Se o estado for `unauthorized`, informa um pedido de autorização no telemóvel.
- Se houver vários dispositivos autorizados, pergunta qual usar.
- Nunca abre duas instâncias do scrcpy ao mesmo tempo para o mesmo dispositivo.

## Publicar a tua própria release (como o Claude Code)

1. Cria um repositório público no GitHub chamado `phone-debug` e executa:

```powershell
git init
git add .
git commit -m "Inicial"
git branch -M main
git remote add origin https://github.com/SEU-UTILIZADOR/phone-debug.git
git push -u origin main
git tag v0.1.0
git push origin v0.1.0
```

2. Edita `install.ps1` e substitui `<SEU-UTILIZADOR>` pelo teu utilizador GitHub **antes** do primeiro `git push` (senão o instalador não sabe de onde descarregar).

3. O GitHub Action `.github/workflows/release.yml` compila automaticamente
   (`phone-debug.exe` self-contained), faz um zip e anexa tudo a essa release.

4. A partir daí, uma linha instala em qualquer PC Windows:

```powershell
irm https://github.com/SEU-UTILIZADOR/phone-debug/releases/latest/download/install.ps1 | iex
```

### Compilar manualmente

```powershell
dotnet publish PhoneDebug.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

ou via o instalador local (adiciona ao PATH):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\install.ps1
```

## Estrutura

```text
PhoneDebug/
├── PhoneDebug.csproj
├── Program.cs
├── Commands/
│   ├── DevicePicker.cs
│   ├── DevicesCommand.cs
│   ├── MirrorCommand.cs
│   ├── LogsCommand.cs
│   ├── InstallCommand.cs
│   ├── ScreenshotCommand.cs
│   ├── InfoCommand.cs
│   └── RebootCommand.cs
├── Services/
│   ├── AdbService.cs
│   ├── ScrcpyService.cs
│   └── DeviceWatcher.cs
├── Models/
│   └── AndroidDevice.cs
├── .github/workflows/
│   └── release.yml
├── install.ps1          (distribuição web)
├── scripts/
│   └── install.ps1      (compilar local)
└── README.md
```