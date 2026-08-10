# Changelog

## 0.4.0

- Truly portable: unzip anywhere and run. On first use Phone Debug downloads
  the current official `adb` and `scrcpy` builds into `tools\` from their own
  sources (Google / GitHub), so no install, PATH or winget is needed
- adb/scrcpy are no longer installed through winget by the installer; they are
  fetched on demand instead (opt out with `PHONEDEBUG_NO_DOWNLOAD=1`)
- Tool downloads stream progress and land version-pinned, official binaries
- The Windows app gets a redesigned UI: rounded cards, status chips, hover
  states and a cleaner palette
- Releases now include a `SHA256SUMS.txt` so downloads can be verified

## 0.3.0

- Releases are signed with GitHub build provenance - verify with
  `gh attestation verify`
- The installer starts the Windows app when it finishes
- The website carries a stricter Content-Security-Policy
- SECURITY.md and Dependabot are now included

## 0.2.0

- Full solution restructure: shared Core, CLI and a Windows desktop app
- Windows app (`PhoneDebug.exe`) with a simple GUI to connect and mirror
- Guided Wi-Fi pairing with a QR code (`phone-debug connect`)
- Xiaomi / POCO / Redmi devices that ignore injected input are driven with an
  emulated USB keyboard and mouse, selected automatically and remembered
- Duplicate entries (Wi-Fi + USB, or repeated mDNS) shown as one device
- `--light` for a lighter picture over Wi-Fi; `--emulated` / `--standard` to
  choose how the phone is controlled
- Per-user installer and uninstaller
- Tests for core services (97 passing)

## 0.1.0

- Android device detection
- Automatic screen mirroring
- CLI (`phone-debug`)
- Windows GUI (`PhoneDebug.exe`)
- APK installation
- Logcat
- Screenshots
- Guided connection: USB instructions, and Wi-Fi pairing with a QR code
  (`phone-debug connect`, or "Connect a phone" in the Windows app)
- Phones that refuse injected input (Xiaomi, POCO, Redmi) are driven with an
  emulated USB keyboard and mouse instead, switched to automatically and
  remembered for next time
- One phone attached twice (USB and Wi-Fi, or a duplicate mDNS registration)
  is shown as a single device
- `--light` for a smaller, smoother picture over Wi-Fi
- `--emulated` / `--standard` to choose how the phone is controlled
- Shared core shared by both front ends
- Installer and uninstaller for the current user
