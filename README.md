# tc_format

`tc_format` is an opinionated formatter built specifically for Structured Text
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
  lines, token spacing, and final newlines. Its structural blank-line defaults
  follow the SPT v4 libraries for `IF` and `CASE`, while every boundary remains
  individually configurable.
- Aligns declarations, direct addresses, initializers, assignments, and named
  call arguments.
- Wraps calls, array and structure initializers, and binary expressions using a
  configurable soft line-length limit.
- Resolves formatting rules from standard `.editorconfig` files, including
  inheritance and per-directory overrides.
- Validates an entire CLI operation before writing and atomically replaces each
  changed file, avoiding partial results caused by invalid source or
  configuration.

An opinionated [`examples/.editorconfig`](examples/.editorconfig) is provided as
an easily modified starting point. See the guides below to install the tool,
configure a project, and choose an integration workflow.

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
