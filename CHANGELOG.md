# Changelog

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
