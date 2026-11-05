# Rugby League Club Manager 1.0 — builds

| File | Platform | Notes |
| --- | --- | --- |
| `RugbyLeagueClubManager-1.0.apk` | Android 12+ (API 32) | Release APK, signed with the project release key. Install with `adb install -r`. |
| `RugbyLeagueClubManager-1.0.aab` | Android 12+ | App Bundle for Google Play upload. |
| `RugbyLeagueClubManager-1.0.ipa` | iOS 18+ | Store-ready IPA for App Store Connect / TestFlight. Signed with the Apple Distribution identity and the `rel-rugby` App Store provisioning profile (no `get-task-allow`). |
| `RugbyLeagueClubManager-1.0-ios-device.zip` | iOS 18+ | Unpackaged release `.app`, signed with a development identity for direct installs. Re-sign with your own distribution profile before submitting. |
| `RugbyLeagueClubManager-1.0-osx-arm64.zip` | macOS (Apple silicon) | Desktop build; unzip and run `RugbyLeague.DesktopGL`. Needs the .NET 10 runtime. |
| `desktop-osx-arm64/` | macOS | The unzipped desktop build. |

## Store assets

Screenshots, preview video and listing copy are in [`store/`](../store), with sizes documented in
[`store/README.md`](../store/README.md).

## Signing

No keystore or Apple certificate is stored in this repository. To build your own signed Android
release, create a keystore and pass it to `dotnet publish` (see
[docs/building.md](../docs/building.md#android)). For iOS, copy `LocalSigning.props.example` to
`LocalSigning.props` and fill in your own `CodesignKey` and `CodesignProvision`.

## Building these artefacts

```bash
dotnet publish src/RugbyLeague.DesktopGL -c Release -r osx-arm64 --self-contained false \
  -o Releases/desktop-osx-arm64

dotnet publish src/RugbyLeague.Android -c Release -p:AndroidPackageFormat=apk \
  -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=<your>.keystore -p:AndroidSigningKeyAlias=rugby

dotnet build src/RugbyLeague.iOS -c Release -p:RuntimeIdentifier=ios-arm64 -p:ValidateXcodeVersion=false

# Signed IPA for the App Store (needs the profile installed on this Mac)
rm -rf /tmp/ipa && mkdir -p /tmp/ipa/Payload
cp -R src/RugbyLeague.iOS/bin/Release/net10.0-ios/ios-arm64/RugbyLeague.iOS.app /tmp/ipa/Payload/
(cd /tmp/ipa && zip -qry "$OLDPWD/Releases/iOS/RugbyLeagueClubManager-1.0.ipa" Payload)
```

The IPA was built with `-p:CodesignKey="Apple Distribution: Paul Johnson (3UH7BE38T3)"`
`-p:CodesignProvision="rel-rugby"`; override both for your own team.
