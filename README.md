# tc_format

`tc_format` formats Structured Text source files in [Beckhoff TwinCAT 3][beckhoff-twincat]
PLC projects, including code embedded in TwinCAT XML files. Use it from the command
line or the optional TwinCAT XAE extension; both use the same `.editorconfig`.

## Quick start

### 1. Install

Follow the [installation guide](docs/installation.md) to install the formatter
and, optionally, the TwinCAT XAE extension.

### 2. Pick one profile

Choose the style closest to your preference. Each file is ready to use and
includes comments explaining every setting.

| Profile to copy | Style | Preview |
| --- | --- | --- |
| [Less whitespace](examples/less-whitespace.editorconfig) | Compact blocks, aligned columns, multiline `)` and `]` after the last item | [Example 1](docs/configuration.md#example-1-less-whitespace) |
| [More whitespace](examples/more-whitespace.editorconfig) | Blank lines around blocks and after multiline calls, aligned columns, multiline `)` and `]` on their own line | [Example 2](docs/configuration.md#example-2-more-whitespace) |
| [More whitespace without assignment alignment](examples/more-whitespace-without-assignment-alignment.editorconfig) | More whitespace, with single spaces around declaration `:`, assignment `:=`, and named parameter `:=` / `=>` operators | [Example 3](docs/configuration.md#example-3-more-whitespace-without-assignment-alignment) |

To customize the style, follow the [configuration walkthrough](docs/configuration.md#build-your-own-configuration)
and edit the annotated settings in your `.editorconfig`.

### 3. Add it to your project

Place `.editorconfig` in the folder containing your PLC project (`.plcproj`),
or a parent folder covering the projects you want to format.

- **New `.editorconfig`:** copy the entire chosen profile into that folder and
  name it exactly `.editorconfig`.
- **Existing `.editorconfig`:** copy the profile's section starting at
  `[*.{st,iecst,TcPOU,TcDUT,TcGVL,TcITF,TcPRG}]`, including all settings and comments
  below it. If that section already exists, merge the settings into it, replacing
  matching properties. Keep unrelated sections and the existing `root` setting.

The example filenames are not discovered automatically. When creating a nested
`.editorconfig` that should inherit parent settings, omit `root = true`.
Profiles also ship in the installer and portable archive's `examples` directory.

### 4. Format your code

With the TwinCAT XAE extension installed:

- **Keyboard:** in the Structured Text editor, press `Ctrl+R`, then `Ctrl+F`
  (the [default shortcut](docs/xae-shortcut.md#keyboard-shortcut)).
- **Editor right-click:** choose **Format Active TwinCAT Structured Text** to
  format the active item's declaration and implementation.
- **Solution Explorer right-click:** select a source file, folder, or project
  and choose **Format Structured Text**. Folders and projects include supported
  files in subfolders. See [Format from Solution Explorer](docs/xae-shortcut.md#format-from-solution-explorer).

Or, use these common CLI commands from your project folder. Folder formatting
includes supported source files in all subfolders and uses each file's
`.editorconfig` settings.

```powershell
# Format the current folder and its subfolders in place.
tc_format .

# Format a specific folder (quote paths containing spaces).
tc_format "C:\Projects\My PLC Project"

# Check a folder without changing any files.
tc_format --check .

# Format one file.
tc_format "POUs\MAIN.TcPOU"

# Show available commands.
tc_format --help
```

See [command-line usage](docs/cli.md) for supported file types, excluded
directories, and exit codes.

## Documentation

| Guide | Contents |
| --- | --- |
| [Installation](docs/installation.md) | Installer, PATH setup, and source builds |
| [Command-line usage](docs/cli.md) | Commands, supported inputs, validation behavior, and exit codes |
| [Configuration](docs/configuration.md) | `.editorconfig`, inheritance, overrides, and supported options |
| [TwinCAT XAE integration](docs/xae-shortcut.md) | Editor, format-on-save, keyboard, and Solution Explorer commands |
| [Git pre-commit integration](docs/pre-commit.md) | Repository hook setup |
| [Development and contributing](CONTRIBUTING.md) | Roslyn conventions, build, test, and publish commands |
| [GitHub builds and releases](docs/releasing.md) | CI and release artifacts |

## License

`tc_format` is available under the [MIT License](LICENSE).

[beckhoff-twincat]: https://www.beckhoff.com/en-us/products/automation/twincat/
