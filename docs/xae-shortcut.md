# TwinCAT XAE integration

The Windows installer can install the `tc_format` XAE extension alongside the
CLI. Close TwinCAT XAE before installing, upgrading, or uninstalling. On the
installer's **Select Components** page, leave **TwinCAT XAE editor integration**
selected. The option is shown only when a compatible XAE Shell installation is
detected.

The installer deploys the unpacked extension into XAE's machine-wide extension
directory:

```text
C:\Program Files\Beckhoff\TcXaeShell\Common7\IDE\Extensions\TcFormat.Xae\
```

Administrator permission is therefore required. After copying or removing the
extension, setup runs `TcXaeShell.exe /setup` to rebuild XAE's command and
extension caches. The bundled XAE `VSIXInstaller.exe` is not required.

After installation, open a Structured Text editor and choose:

```text
Tools → Format Active TwinCAT Structured Text
```

TwinCAT exposes a POU or method's declaration and implementation as separate
sections of one automation-model item. The extension sends both sections of the
active item to the installed CLI and replaces their in-memory text with the
results. It does not save the document or modify the backing file directly, so
no external-change reload is needed. If both sections are already formatted,
the command does not mark the document as modified.

The same command appears as **Format Active TwinCAT Structured Text** on the
Structured Text editor's right-click menu.

The backing file path is used to resolve `.editorconfig`, including parent
configuration files and project-specific overrides. Formatter errors are shown
in a dialog and written to the `tc_format` pane in XAE's Output window; the
editor is left unchanged.

## Format on save

Format-on-save is disabled by default. To enable it:

1. Open **Tools → Options**.
2. Expand **tc_format** and select **General**.
3. Set **Format on save** to `True`, then choose **OK**.

The extension formats the focused declaration or implementation half
immediately before XAE saves the active Structured Text editor, so the formatted
text is written in the same save operation. It does not run against unrelated
files during **Save All** and it does not format unsupported editor types. If
formatting fails, XAE saves the original text and the error is written to the
`tc_format` Output pane.

Format-on-save is an extension preference stored by XAE. Formatting rules stay
in the project's `.editorconfig`, so manual formatting, format-on-save, the
CLI, and pre-commit checks all produce the same result.

## Format from Solution Explorer

Right-click a supported TwinCAT source file, folder, or project in Solution
Explorer and choose **Format Structured Text**. File selections format that
file; folder and project selections recursively format supported source files
beneath the selected location. Multiple selected items are passed to one CLI
operation, so the CLI stages and validates all changes before writing any file.

TwinCAT methods are stored inside their containing `.TcPOU` rather than as
separate files. Selecting a method resolves TwinCAT's virtual method path to
that backing `.TcPOU`, so **Format Structured Text** formats the complete source
file containing the method.

Solution Explorer formatting operates on files because the selection can span
documents that are not open. Before invoking the CLI, the extension checks all
open supported documents covered by the selection. If any have unsaved changes,
the complete operation is canceled and the affected paths are listed. Save or
revert those documents and run the command again.

## Keyboard shortcut

The extension uses `Ctrl+R, Ctrl+F` by default. This is a two-step chord: press
`Ctrl+R`, release it, and then press `Ctrl+F`.

To choose a different preset:

1. Open **Tools → Options**.
2. Expand **tc_format** and select **General**.
3. Set **Format shortcut** to `Ctrl+R, Ctrl+F` or any single-key preset from
   `Ctrl+1` through `Ctrl+9`, then choose **OK**.

The change takes effect immediately. The extension keeps the preset bindings
separate from Visual Studio's keyboard scheme and activates only the selected
formatter command, so it does not need to rewrite the default scheme. The
selection is stored in XAE's user settings and remains active after restarting
XAE.

To assign a shortcut outside the preset list:

1. Open **Tools → Options → Environment → Keyboard**.
2. Search **Show commands containing** for `tc_format`.
3. Select `Tools.tc_format.FormatActiveDocument`.
4. Put the cursor in **Press shortcut keys** and press the desired combination,
   such as `Ctrl+K, Ctrl+D`.
5. Choose **Assign**, then **OK**.

The Visual Studio-standard `Ctrl+K, Ctrl+D` chord is already assigned to
**Edit.FormatDocument**. Reusing it for tc_format can cause a conflict unless
that existing binding is removed or the commands are assigned to distinct
scopes. The Keyboard page shows any conflict before assignment. A manually
assigned shortcut is additional to the preset selected on the General page.

The formatter does not bind the standalone `Ctrl+F` shortcut, which remains
available for XAE's **Find** command.

## Verify the extension

1. Open a `.TcPOU`, `.TcDUT`, or `.TcGVL` implementation or declaration.
2. Make a harmless spacing change without saving.
3. Press the preset selected under **tc_format → General** and confirm that both
   sections are formatted without an external-file reload.
5. If format-on-save is enabled, repeat the spacing change and save. Confirm the
   editor is formatted and is no longer marked as modified.

Use **View → Output** and select the `tc_format` pane when diagnosing a failure.

## CLI-only fallback

If the extension component is not installed, the formatter can still be added
under **Tools → External Tools** with command `tc_format.exe` and arguments
`"$(ItemPath)"`. That workflow formats the saved file and may require XAE to
reload it; the installed extension is preferred for normal editing.
