# NextUpKids

**[Download the latest Android APK](https://github.com/Romain-OD/NextUpKids/releases/latest/download/NextUpKids.apk)** (allow installs from unknown sources).

## Release

Push a tag `vX.Y.Z`. The `android` workflow tests, builds the signed APK (versionName `X.Y.Z`) and attaches it to the `vX.Y.Z` release.

```
git tag v1.0.0; git push origin v1.0.0
```

Signing uses the `ANDROID_KEYSTORE_BASE64` and `ANDROID_KEYSTORE_PASSWORD` repo secrets (alias `nextupkids`). Keep a backup of the keystore: a lost key means users must uninstall to update.
