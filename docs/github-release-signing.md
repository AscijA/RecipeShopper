# GitHub release signing

The `Signed mobile release` workflow builds a signed Android APK, Android App Bundle, and iOS IPA, then attaches all three files to a GitHub Release.

## Android repository secrets

- `ANDROID_KEYSTORE_BASE64`: Base64-encoded Android keystore file.
- `ANDROID_KEY_ALIAS`: Alias of the signing key in the keystore.
- `ANDROID_KEY_PASSWORD`: Password for the signing key.
- `ANDROID_STORE_PASSWORD`: Password for the keystore.

Create a production keystore once and keep the original plus its passwords in a secure backup. Losing it can prevent future updates of an already-published Android app.

Encode it on Windows:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\secure\moji-recepti.keystore")) | Set-Clipboard
```

## iOS repository secrets

- `IOS_CERTIFICATE_BASE64`: Base64-encoded Apple Distribution `.p12` certificate.
- `IOS_CERTIFICATE_PASSWORD`: Export password of the `.p12` file.
- `IOS_CODESIGN_KEY`: Full certificate identity, such as `Apple Distribution: Company Name (TEAMID)`.
- `IOS_PROVISIONING_PROFILE_BASE64`: Base64-encoded App Store or Ad Hoc `.mobileprovision` file for `ba.mojirecepti.app`.
- `IOS_PROVISIONING_PROFILE_NAME`: The profile name shown in the Apple Developer portal.
- `IOS_KEYCHAIN_PASSWORD`: A strong temporary password used only for the CI keychain.

Encode the certificate and profile on macOS:

```bash
base64 -i distribution.p12 | pbcopy
base64 -i MojiRecepti.mobileprovision | pbcopy
```

The Apple certificate and provisioning profile must be current and must both belong to the same Apple Developer team.

## Add secrets

Open the private repository and go to **Settings > Secrets and variables > Actions > New repository secret**. Add every secret listed above. Secrets are never stored in this repository.

## Create a release

Either push a semantic-version tag:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Or run **Actions > Signed mobile release > Run workflow** and enter a new tag such as `v1.0.0`.

The workflow tests the solution first. A release is published only after both signed platform builds succeed. Downloadable `.apk`, `.aab`, and `.ipa` files then appear under the release.
