# Installation

## Windows installer

Download `tc_format-VERSION-win-x64-setup.exe` from the GitHub release, close
TwinCAT XAE, and run the installer. Windows requests administrator permission
because the XAE extension is installed into XAE's machine-wide extension
directory. The CLI itself is installed to:

```text
%ProgramFiles%\tc_format\
```

The CLI component adds that directory to the system `Path` and records
the executable location for the XAE extension. When a compatible TwinCAT XAE
Shell is present, the default installation also installs the editor extension.
Choose **CLI only** on the component page if that integration is not wanted.
When the XAE component is selected, setup detects a running `TcXaeShell.exe`
and requires every XAE window to be closed before it changes the extension.
It then rebuilds XAE's extension cache with `TcXaeShell.exe /setup`. The same
running-process check and cache rebuild protect uninstallation.

Open a new terminal after setup and verify:

```powershell
where.exe tc_format
tc_format --version
```

Uninstalling removes the XAE extension, the application files, and only the
matching `tc_format` directory entry from the system PATH. Existing PATH entries
are preserved. Keep XAE closed during uninstall so its extension can be removed
cleanly.

## Build from source

Publish the self-contained executable with:

```powershell
dotnet publish src\TcFormat.Cli -c Release -r win-x64 --self-contained `
  -o artifacts\publish\win-x64
```

The executable is produced under:

```text
artifacts\publish\win-x64\tc_format.exe
```

It contains the .NET runtime and does not require a separate runtime
installation on the target workstation.

For a temporary manual installation, copy `tc_format.exe` to:

```text
%LOCALAPPDATA%\Programs\tc_format\
```

Add that exact directory to the current user's `Path` through Windows
**Environment Variables**, then open a new terminal and verify:

```powershell
where.exe tc_format
tc_format --version
```

To build the installer locally, install [Inno Setup 6 or 7](https://jrsoftware.org/isdl.php)
and run:

```powershell
.\installer\build.ps1 -Version 1.0.0.0
```

With no `-PublishDirectory`, the script republishes the self-contained CLI so a
stale executable cannot be packaged. It also builds the XAE VSIX and writes the
combined installer beneath `artifacts\installer`. Pass `-PublishDirectory` only
to package an executable that was published separately. The GitHub release
workflow performs a CLI-only install test because its runner does not have
TwinCAT XAE; XAE integration is verified on a workstation that has the
compatible shell installed.
