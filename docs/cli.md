# Command-line usage

The `tc_format` command can format one or more files or directories in a single
operation. Install it first by following the [installation guide](installation.md),
then open a new terminal so the updated `PATH` is available.

Run the command without arguments, or pass `--help`, to display the built-in
usage summary:

```powershell
tc_format --help
```

| Invocation | Purpose |
| --- | --- |
| `tc_format FILE\|DIRECTORY ...` | Format one or more files or directories in place |
| `tc_format --check FILE\|DIRECTORY ...` | Report files that need formatting without writing them |
| `tc_format --stdin-filepath FILE` | Format standard input using the configuration for `FILE` |
| `tc_format --version` | Display the installed version |
| `tc_format --help` | Display built-in help |

## Format files and directories

Pass any combination of supported source files and directories:

```powershell
# Format one file.
tc_format .\PlcProject\POUs\Main.TcPOU

# Recursively format every supported file beneath a directory.
tc_format .\PlcProject

# Process several inputs as one validated operation.
tc_format .\PlcProjectA .\PlcProjectB\Globals.TcGVL
```

Directory searches are recursive. The formatter skips `.git`, `.vs`, `_Boot`,
`_CompileInfo`, `bin`, and `obj` directories and ignores files with unsupported
extensions. Duplicate files reached through overlapping input paths are
processed only once.

Supported inputs are:

| Kind | Extensions | Behavior |
| --- | --- | --- |
| Plain Structured Text | `.st`, `.iecst` | Formats the complete file |
| TwinCAT XML source | `.TcPOU`, `.TcDUT`, `.TcGVL`, `.TcITF`, `.TcPRG` | Formats Declaration and Structured Text CDATA regions while preserving the surrounding XML |

Extension matching is case-insensitive. Files that are already formatted are
left untouched. If no supported files are found, the command reports that fact
and exits successfully.

Each file resolves its own effective `.editorconfig` settings. This allows one
command to process multiple projects or directories with different overrides.
See [Configuration](configuration.md) for discovery rules and the complete set
of formatting options.

The formatter applies an opinionated structural blank-line policy:

- Add one blank line before `VAR` and `VAR_*` blocks, except when the block
  immediately follows a `METHOD` declaration.
- Keep `IF` blocks tight before `IF`, `ELSIF`, an IF-context `ELSE`, and
  `END_IF`, and after the `THEN` on `IF` and `ELSIF` lines.
- Keep `CASE` blocks tight before `CASE` and `END_CASE`, but add one blank line
  before a CASE-context `ELSE`.
- Remove blank lines immediately after a `METHOD`, variable-block opener, or
  `ELSE`.

Each boundary has an individual `tc_format_blank_line_*` setting. Use `true`
to require a blank line, `false` to remove it, or `preserve` to leave that
boundary unchanged; see [Configuration](configuration.md) for the complete
list.

## Check formatting without writing

Place `--check` before the input paths to verify formatting without changing
any files:

```powershell
tc_format --check .\PlcProject
tc_format --check .\PlcProject\POUs\Main.TcPOU .\Libraries
```

The command prints each file that would be reformatted and returns exit code
`1` if it finds any. This mode is suitable for pre-commit hooks and CI/CD jobs;
see [Git pre-commit integration](pre-commit.md) for a ready-to-use example.

## Display the installed version

Use `--version` to verify which build is available on `PATH`:

```powershell
tc_format --version
```

## Standard-input mode

Editor integrations can send raw Structured Text through standard input:

```powershell
Get-Content -Raw .\snippet.st |
  tc_format --stdin-filepath .\PlcProject\POUs\Main.TcPOU
```

`--stdin-filepath` accepts exactly one path. The path selects the applicable
`.editorconfig` rules; the command does not read from or write to that backing
file. Formatted text is written to standard output, while diagnostics are
written to standard error.

This mode is primarily an integration interface. To format a saved source file
in place, pass its path as a normal positional argument instead.

## Validation and file safety

For a normal formatting operation, `tc_format` first discovers every input,
loads and validates all supported source documents, resolves their
configuration, and stages the formatted results in memory. If any source or
configuration error is found, it reports the diagnostics and writes no files.

After successful validation, each changed file is replaced atomically. The
formatter preserves UTF-8 (with or without a byte-order mark) and BOM-marked
UTF-16 little- or big-endian encodings. For TwinCAT XML source, it also checks
that the document is well formed before formatting its code regions.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Formatting succeeded, no supported files were found, or `--check` found no changes |
| `1` | `--check` found one or more files that would be reformatted |
| `2` | The input, configuration, or source text was invalid |

Scripts should distinguish exit code `1` from `2`: the first means formatting
is required, while the second means the operation could not be completed and
its diagnostics should be reviewed.
