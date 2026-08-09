# tools

Optional. Phone Debug looks here **first** for `adb.exe` and `scrcpy.exe`,
before your PATH and before the usual install locations.

Use it when you want a self-contained, portable copy - a USB stick, a locked-down
machine, or a build agent - instead of installing the tools system-wide.

## Layout

Either of these works:

```text
tools\
  adb.exe                  (plus AdbWinApi.dll and AdbWinUsbApi.dll)
  scrcpy.exe               (plus the DLLs and scrcpy-server that ship with it)
```

```text
tools\
  platform-tools\adb.exe
  scrcpy\scrcpy.exe
```

Copy the *whole* folder of each tool - both need the files that ship alongside
them (`scrcpy-server` in particular, or mirroring fails).

## Where to get them

```powershell
winget install Google.PlatformTools
winget install Genymobile.scrcpy
```

They are not included in Phone Debug releases; see
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) for why.

## Overriding a single tool

Environment variables win over everything, including this folder:

```powershell
$env:PHONEDEBUG_ADB    = "D:\android\platform-tools\adb.exe"
$env:PHONEDEBUG_SCRCPY = "D:\scrcpy\scrcpy.exe"
```
