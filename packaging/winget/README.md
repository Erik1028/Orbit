# winget

Manifests for the Windows Package Manager, so OrbitOverlay can be installed with

```
winget install Erik1028.OrbitOverlay
```

They are not in the winget catalogue until a pull request to
[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) is merged.

## First submission

1. Fork `microsoft/winget-pkgs` on GitHub.
2. Copy `manifests/e/Erik1028/OrbitOverlay/1.12.3/` into the same path in your fork.
3. Open a pull request. A bot installs the package in a sandbox and checks it; a moderator merges it.

Or, on a Windows PC with [wingetcreate](https://github.com/microsoft/winget-create):

```
wingetcreate submit packaging\winget\manifests\e\Erik1028\OrbitOverlay\1.12.3
```

## Each new release

```
wingetcreate update Erik1028.OrbitOverlay --version 1.12.4 ^
  --urls https://github.com/Erik1028/OrbitOverlay/releases/download/v1.12.4/OrbitOverlaySetup.exe --submit
```

This computes the new SHA-256 itself. It can also run in a GitHub Action on every release, with a personal access
token stored as a repository secret.

## Notes

- The installer is the app's own exe. `--setup` makes it run as an installer whatever the downloaded file is called,
  and `--quiet` installs without the window, for the current user, to `%LOCALAPPDATA%\Programs\OrbitOverlay`.
- `ProductCode: Orbit` is the key under `HKCU\...\Uninstall` that the installer writes, so winget can find an
  existing install and uninstall it (`QuietUninstallString` is `--uninstall --quiet`).
