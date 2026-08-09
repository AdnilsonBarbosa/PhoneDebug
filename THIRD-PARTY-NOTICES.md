# Third-party notices

Phone Debug drives two external programs: **adb** and **scrcpy**. It does not
ship either of them in its release package, and it does not embed them in its
executables. This page explains why, and how they get on to your machine.

## How the tools arrive

On first use, Phone Debug checks whether `adb` and `scrcpy` can be found. When
one is missing, it downloads the **official** binary from the project's own
source straight onto your machine and unpacks it into the `tools` folder next
to Phone Debug:

- **adb** is downloaded as part of Google's `platform-tools-latest-windows.zip`
  from <https://dl.google.com/android/repository/>.
- **scrcpy** is downloaded from the project's GitHub releases
  (<https://github.com/Genymobile/scrcpy/releases>).

Because the download happens on the machine that runs Phone Debug, Phone Debug
itself is not a distributor of those binaries - you are, as the person who runs
the download. That is what keeps the licence position straightforward.

You can opt out at any time (e.g. on a locked-down machine that already has the
tools) with `$env:PHONEDEBUG_NO_DOWNLOAD = "1"`.

## The licences that apply

The download is only the easy part; what you may *do* with the result is
covered by each project's own licence:

| Component | Licence | Source |
| --- | --- | --- |
| Android SDK Platform-Tools (`adb`) | Android Software Development Kit License Agreement | <https://developer.android.com/studio/terms> |
| scrcpy | Apache-2.0 | <https://github.com/Genymobile/scrcpy> |
| FFmpeg (inside scrcpy builds) | LGPL-2.1+ | <https://ffmpeg.org> |
| SDL (inside scrcpy builds) | zlib | <https://libsdl.org> |

## Why they are not bundled

**adb** is part of the Android SDK Platform-Tools, distributed under the
*Android Software Development Kit License Agreement*. That agreement does not
grant a right to redistribute the SDK or parts of it, so Phone Debug cannot
include `adb.exe` in a release.

**scrcpy** is licensed under the **Apache License 2.0**, which *does* allow
redistribution, but:

- the official Windows build of scrcpy itself contains `adb.exe`, plus FFmpeg
  and SDL binaries under their own licences (LGPL-2.1 and zlib), each of which
  carries its own redistribution obligations;
- by fetching the current release from GitHub on first run, every user gets
  the latest, signed upstream build with no stale copies going stale in ours.

## Using your own copies (portable mode)

If you would rather manage the tools yourself, put `adb.exe` and/or
`scrcpy.exe` (with the files they need) in the `tools` folder next to
`PhoneDebug.exe` and they are used before anything on the PATH - the download
is then skipped because nothing is missing.

## Bundled with Phone Debug

Releases built with the default settings embed the **.NET 9 runtime**
(MIT licence, <https://github.com/dotnet/runtime>) so the executables run on a
machine with no .NET installed.

Phone Debug itself is MIT licensed - see [LICENSE](LICENSE).