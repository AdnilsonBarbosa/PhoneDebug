# tools

`adb.exe` and `scrcpy.exe` look here **first**, before your PATH and before the
usual install locations. This is what makes Phone Debug fully portable: unzip a
release anywhere, run it, and the first launch downloads the tools it needs
into this folder - nothing to install, nothing system-wide.

## What happens on first run

Releases do **not** ship adb or scrcpy - their licences do not allow Phone
Debug to redistribute them. Instead, the first time a command needs a missing
tool, Phone Debug downloads it from the official source:

- **adb**: Google's `platform-tools-latest-windows.zip`
  (dl.google.com / googlesource.com), so it always lands on the current version.
- **scrcpy**: the latest `scrcpy-win64-v*.zip` from the Genymobile/scrcpy
  GitHub release (which also brings its own adb).

Both are placed in this folder, which keeps them out of your PATH and makes the
whole install self-contained. Downloading the official binaries on your own
machine - rather than bundling them - is what keeps the licence position clean.
See [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

## Layout

After the first run the folder looks like:

```text
tools\
  platform-tools\adb.exe        (plus the DLLs that ship with adb)
  scrcpy\scrcpy.exe             (plus the DLLs, scrcpy-server and adb that ship with it)
```

A flat variant also works, if you drop files in by hand:

```text
tools\
  adb.exe                       (plus AdbWinApi.dll and AdbWinUsbApi.dll)
  scrcpy.exe                    (plus the DLLs and scrcpy-server that ship with it)
```

Copy the *whole* folder of each tool - both need the files that ship alongside
them (`scrcpy-server` in particular, or mirroring fails).

## Skipping the download

A locked-down machine may prefer to supply the tools itself. Set this before
running Phone Debug:

```powershell
$env:PHONEDEBUG_NO_DOWNLOAD = "1"
```

Then Phone Debug behaves as before and only shows install instructions when a
tool is missing.

## Overriding a single tool

Environment variables win over everything, including the downloaded tools:

```powershell
$env:PHONEDEBUG_ADB    = "D:\android\platform-tools\adb.exe"
$env:PHONEDEBUG_SCRCPY = "D:\scrcpy\scrcpy.exe"
```

Pointing one at a missing path is an override too - it never silently falls
back to another copy.
