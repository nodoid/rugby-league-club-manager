# Rugby League Club Manager 1.0 — builds

| File | Platform | Notes |
| --- | --- | --- |
| `RugbyLeagueClubManager-1.0.apk` | Android 12+ (API 32) | Release APK, signed with the project release key. Install with `adb install -r`. |
| `RugbyLeagueClubManager-1.0.aab` | Android 12+ | App Bundle for Google Play upload. |
| `RugbyLeagueClubManager-1.0-ios-device.zip` | iOS 18+ | Release `.app` for a physical device/TestFlight tooling. Signed with a development identity; re-sign with your own distribution profile before submitting. |
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
```
