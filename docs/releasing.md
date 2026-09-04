# GitHub builds and releases

The checked-in GitHub Actions workflows become active when this repository is
pushed to GitHub.

## Continuous integration

`.github/workflows/ci.yml` runs on pull requests and pushes to `main`. It:

1. installs the .NET SDK selected by `global.json`;
2. verifies C# formatting against the repository `.editorconfig`;
3. builds and tests the solution; and
4. checks direct and transitive NuGet packages for known vulnerabilities.

## Release artifacts

`.github/workflows/release.yml` runs manually or for a tag beginning with `v`.
It builds and tests the solution, publishes the self-contained Windows x64
executable, builds the TwinCAT XAE VSIX, compiles both into the Inno Setup
installer, and uploads these artifacts:

- `tc_format-VERSION-win-x64.zip`
- `tc_format-VERSION-win-x64-setup.exe`
- `SHA256SUMS.txt`

Before uploading them, the workflow performs a CLI-only silent installation
into a temporary directory, verifies the system PATH registration, runs
`tc_format --version`, uninstalls it, and verifies PATH cleanup. The XAE
component is unavailable on the GitHub runner and therefore requires a manual
smoke test on a workstation with the supported TwinCAT XAE Shell.

Create a GitHub release by pushing a matching version tag:

```powershell
git tag v1.0.0.0
git push github v1.0.0.0
```

A manually dispatched run uploads workflow artifacts but does not create a
GitHub release. Release installers are currently unsigned, so Windows may show
a publisher or SmartScreen warning until code signing is configured.
