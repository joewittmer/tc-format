# Development and contributing

## Requirements

Development requires the .NET 10 SDK. The Windows installer build additionally
requires Inno Setup 6. Building the XAE extension itself does not require an XAE
installation; running and manually verifying it does.

## C# formatting convention

This project uses the reusable top-level formatting, code-style, and naming
rules from [Roslyn's `.editorconfig`](https://github.com/dotnet/roslyn/blob/main/.editorconfig)
for its own C# source. The rules currently vendored in the repository root were
reviewed against Roslyn on September 4, 2026.

Roslyn-specific directory overrides and analyzer suppressions are not relevant
to this codebase and are excluded. Roslyn's .NET Foundation file header is also
excluded because it does not describe this project's copyright ownership. The
Structured Text profile in `examples/.editorconfig` is a separate formatter
configuration and does not inherit the C# convention.

Format the solution before submitting a change:

```powershell
dotnet format tc_format.slnx
```

CI verifies the same convention with:

```powershell
dotnet format tc_format.slnx --no-restore --verify-no-changes
```

## Build and test

```powershell
dotnet build tc_format.slnx
dotnet test tc_format.slnx
```

The solution build also creates
`src/TcFormat.Xae/bin/Debug/net472/TcFormat.Xae.vsix`. The release installer
build regenerates the VSIX with the requested product version.

## Publish a standalone executable

```powershell
dotnet publish src/TcFormat.Cli -c Release -r win-x64 --self-contained
```

The self-contained publish includes the required .NET runtime. See
[Installation](docs/installation.md) for the installer and PATH behavior and
[GitHub builds and releases](docs/releasing.md) for packaging details.

## Near-term scope

The next formatter milestone is parser-backed wrapping. GitHub Actions already
build, test, package, install-test, and publish the portable and installer
artifacts.
