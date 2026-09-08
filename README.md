# tc_format

`tc_format` is a configurable formatter built specifically for Structured Text
source files in PLC projects created with [Beckhoff TwinCAT 3][beckhoff-twincat].
It is not intended to be a general-purpose IEC 61131-3 formatter.

Use it directly from the command line, enforce formatting with a Git pre-commit
hook or CI/CD pipeline, or install the optional TwinCAT XAE extension for
editor commands and format-on-save. Every entry point uses the same formatter
and project configuration.

## Features

- Formats plain Structured Text files and the code regions embedded in TwinCAT
  XML source files without rewriting the surrounding XML.
- Normalizes indentation, keyword casing, line endings, whitespace, blank
  lines, token spacing, and final newlines.
- Aligns declarations, direct addresses, initializers, assignments, and named
  call arguments. Hanging layouts keep continuation arguments aligned beneath
  the first argument, including nested calls such as `CONCAT`.
- Wraps calls, array and structure initializers, and binary expressions using a
  configurable soft line-length limit.
- Supports blank lines after multiline calls and conditional spacing after
  multiline `IF`, `ELSIF`, `FOR`, and `WHILE` headers, including headers expanded
  by automatic wrapping.
- Resolves formatting rules from standard `.editorconfig` files, including
  inheritance and per-directory overrides.
- Validates an entire CLI operation before writing and atomically replaces each
  changed file, avoiding partial results caused by invalid source or
  configuration.

## Choose a formatting profile

Three complete, annotated profiles are included:

| Profile | Layout |
| --- | --- |
| [Less whitespace](examples/less-whitespace.editorconfig) | Keeps control-flow boundaries compact and removes blank lines after multiline calls. |
| [More whitespace](examples/more-whitespace.editorconfig) | Separates control-flow sections, multiline conditions, and multiline calls with blank lines. Expands multiline arguments with leading operators, adds a blank line after `DO`, and keeps `ELSE` tight. |
| [More whitespace without assignment alignment](examples/more-whitespace-without-assignment-alignment.editorconfig) | Uses the more-whitespace layout with single spaces around `:=` and `=>`, without padding assignments, declaration initializers, or named parameters into columns. |

All use four-space indentation, hanging call alignment, and `always` initializer
wrapping, which puts each array entry or structure field on a continuation line.
The third profile disables declaration-colon, `:=`, and `=>` alignment. Each option has
comments explaining its purpose, accepted values, and built-in default; the
selected profile values may differ from those defaults.

Copy the chosen file to your Structured Text project root as `.editorconfig`,
or merge its section into an existing `.editorconfig`. The example filenames
are not discovered automatically. The installer and portable archive include
all three profiles in their `examples` directory.

For example, the more-whitespace profile produces this layout:

```iecst
IF ready THEN
    _axis.Enable();
    _axis.MoveAbsolute(Position := targetPosition,
                       Velocity := 20);

    moving := TRUE;

ELSIF waiting AND
    connectionHealthy THEN

    _axis.Reset();

ELSE
    _axis.Stop();

END_IF
```

Use the profiles themselves as the complete option references. The
[configuration guide](docs/configuration.md) explains profile differences,
initializer layouts, inheritance, and how interacting settings are resolved.

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
