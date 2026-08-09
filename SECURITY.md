# Security

## Reporting a vulnerability

Please **do not open a public issue** for security problems.

Use GitHub's private vulnerability reporting:

1. Open [the repository](https://github.com/AdnilsonBarbosa/PhoneDebug)
2. Go to **Security** -> **Report a vulnerability**
3. Describe the issue, how to reproduce it, and what impact it has

Thank you for helping keep it safe.

## How releases are secured

- Every release is built from a git tag on `main` by a locked-down GitHub
  Actions workflow (least-privilege permissions, build provenance).
- Every artefact (zip and both executables) carries **build provenance
  attestations**. You can verify a file was built by this repository:

  ```powershell
  gh attestation verify .\phone-debug.exe --owner AdnilsonBarbosa
  gh attestation verify .\PhoneDebug.exe --owner AdnilsonBarbosa
  gh attestation verify .\PhoneDebug-v*.zip --owner AdnilsonBarbosa
  ```

- Nothing here requests administrator rights. Installation is per-user.
- adb and scrcpy are separate projects with their own licences - see
  [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).