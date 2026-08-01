# GitHub release signing

The `Personal mobile release` workflow builds a signed Android APK, Android App Bundle, and unsigned iOS device IPA, then attaches all three files to a GitHub Release. Sideloadly signs the IPA locally with a free Apple Account when installing it on an iPhone.

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

## iOS with Sideloadly

No iOS repository secrets or paid Apple Developer membership are required. Download `MojiRecepti-unsigned.ipa` from the GitHub Release, open it in Sideloadly on Windows, connect the iPhone, and use Apple ID sideload mode. With a free Apple Account, Apple requires the app to be refreshed every seven days. Sideloadly's automatic refresh can do this while the PC and iPhone can see each other over USB or Wi-Fi.

## Add secrets

Open the private repository and go to **Settings > Secrets and variables > Actions > New repository secret**. Add the four Android secrets listed above. Secrets are never stored in this repository.

## Create a release

Either push a semantic-version tag:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Or run **Actions > Personal mobile release > Run workflow** and enter a new tag such as `v1.0.0`.

The workflow tests the solution first. A release is published only after both platform builds succeed. Downloadable `.apk`, `.aab`, and Sideloadly-ready `.ipa` files then appear under the release.
