# Git pre-commit integration

Install `tc_format` and confirm it is available on `PATH` before configuring the
hook. Add this check-only hook to `.pre-commit-config.yaml`:

```yaml
repos:
  - repo: local
    hooks:
      - id: tc-format-check
        name: Check TwinCAT Structured Text formatting
        entry: tc_format --check
        language: unsupported
        files: '(?i)\.(st|iecst|tcpou|tcdut|tcgvl|tcitf|tcprg)$'
```

Then install and exercise the hook:

```powershell
pre-commit install
pre-commit run --all-files
```

The hook is intentionally check-only. If it reports changes, run:

```powershell
tc_format .
git diff
git add -u
```

`tc_format --check` exits `0` when every file is formatted, `1` when files would
change, and `2` for invalid configuration, invalid source, or I/O errors.

`language: unsupported` is the current pre-commit name for a command supplied by
the local system. Older pre-commit installations may call this language
`system`.

