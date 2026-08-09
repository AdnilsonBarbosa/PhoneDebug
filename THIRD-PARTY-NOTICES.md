# Third-party notices

Phone Debug drives two external programs: **adb** and **scrcpy**. It does not
ship either of them. This page explains why, and what applies if you add them
to the `tools` folder yourself.

## Why adb is not bundled

`adb` is part of the Android SDK Platform-Tools, distributed under the
*Android Software Development Kit License Agreement*. That agreement does not
grant a right to redistribute the SDK or parts of it, so Phone Debug cannot
legally include `adb.exe` in a release.

`install.ps1` installs it from Google's official winget package
(`Google.PlatformTools`) instead, or points you at
<https://developer.android.com/tools/releases/platform-tools>.

## Why scrcpy is not bundled

scrcpy is licensed under the **Apache License 2.0**, which does allow
redistribution. It is still not bundled, because:

- scrcpy needs `adb` anyway, which cannot be redistributed - a bundle would be
  half a solution;
- the official Windows build of scrcpy itself contains `adb.exe`, plus FFmpeg
  and SDL binaries under their own licences (LGPL-2.1 and zlib), each of which
  carries its own redistribution obligations;
- installing it from `Genymobile.scrcpy` via winget keeps you on upstream's
  signed, updatable build.

## Using your own copies (portable mode)

Put `adb.exe` and/or `scrcpy.exe` (with the files they need) in the `tools`
folder next to `PhoneDebug.exe` and they are used before anything on the PATH.

If you then redistribute that folder, you are the one distributing those
binaries, and their licence terms apply to you:

| Component | Licence | Source |
| --- | --- | --- |
| Android SDK Platform-Tools (`adb`) | Android SDK License Agreement | <https://developer.android.com/studio/terms> |
| scrcpy | Apache-2.0 | <https://github.com/Genymobile/scrcpy> |
| FFmpeg (inside scrcpy builds) | LGPL-2.1+ | <https://ffmpeg.org> |
| SDL (inside scrcpy builds) | zlib | <https://libsdl.org> |

## Bundled with Phone Debug

Releases built with the default settings embed the **.NET 9 runtime**
(MIT licence, <https://github.com/dotnet/runtime>) so the executables run on a
machine with no .NET installed.

Phone Debug itself is MIT licensed - see [LICENSE](LICENSE).
