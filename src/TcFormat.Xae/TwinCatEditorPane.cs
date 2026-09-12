using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace TcFormat.Xae;

// TwinCAT's IVsTextLines is a synthesized search buffer spanning multiple
// sections. Use the real pane's text and undo operation instead.
internal sealed class TwinCatEditorPane
{
    private const string ControlTypeName = "_3S.CoDeSys.Controls.Controls.SynEdControl";
    private readonly Control control;
    private readonly object buffer;
    private readonly MethodInfo undo;

    private TwinCatEditorPane(Control control)
    {
        this.control = control;
        buffer = GetProperty(control, "Buffer")
            ?? throw new InvalidOperationException("TwinCAT's editor buffer is unavailable.");
        undo = control.GetType().GetMethod("Undo", Type.EmptyTypes)
            ?? throw new InvalidOperationException("TwinCAT's editor undo operation is unavailable.");
    }

    internal static TwinCatEditorPane? TryCreate(object documentView, Action<string>? trace)
    {
        if (documentView.GetType().FullName != "Beckhoff.TwinCAT.VS.VSEditor")
        {
            return null;
        }

        var editor = GetProperty(documentView, "EditorView")
            ?? throw new InvalidOperationException("TwinCAT's document editor is unavailable.");
        return FromEditor(editor, trace);
    }

    internal static TwinCatEditorPane FromEditor(object editor, Action<string>? trace = null)
    {
        var root = GetProperty(editor, "Control") as Control
            ?? throw new InvalidOperationException("TwinCAT's document control is unavailable.");
        var candidates = FindTextControls(root).ToArray();
        var focused = candidates.Where(candidate => candidate.ContainsFocus).ToArray();
        if (focused.Length == 1)
        {
            trace?.Invoke("Selected the focused TwinCAT text control.");
            return new TwinCatEditorPane(focused[0]);
        }

        // Menu and save commands temporarily take keyboard focus. The POU
        // editor retains the selected section in InterfaceActive.
        if (GetProperty(editor, "InterfaceActive") is bool declarations)
        {
            var paneName = declarations ? "InterfaceEditorView" : "ImplementationEditorView";
            var pane = GetProperty(editor, paneName);
            var paneRoot = pane is null ? null : GetProperty(pane, "Control") as Control;
            candidates = paneRoot is null
                ? Array.Empty<Control>()
                : FindTextControls(paneRoot).Where(candidate => candidates.Contains(candidate)).ToArray();
            trace?.Invoke($"Selected TwinCAT {(declarations ? "declaration" : "implementation")} pane.");
        }

        if (candidates.Length != 1)
        {
            throw new InvalidOperationException("Cannot identify one active TwinCAT text pane. No changes were applied.");
        }

        return new TwinCatEditorPane(candidates[0]);
    }

    internal bool IsSamePane(TwinCatEditorPane other) =>
        ReferenceEquals(control, other.control) && ReferenceEquals(buffer, other.buffer);

    internal string GetText()
    {
        EnsureAvailable();
        return control.Text;
    }

    internal void ReplaceText(string original, string formatted, Action<string>? trace,
        Func<string, string>? saveOriginal = null)
    {
        EnsureAvailable();
        if (!string.Equals(control.Text, original, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The TwinCAT pane changed before formatting could be applied.");
        }

        if (GetProperty(buffer, "Modifiable") is not true)
        {
            throw new InvalidOperationException("The TwinCAT text pane is not modifiable. No changes were applied.");
        }

        if (string.Equals(original, formatted, StringComparison.Ordinal))
        {
            return;
        }

        var recoveryPath = (saveOriginal ?? SaveOriginal)(original);
        trace?.Invoke($"Original pane text saved to {recoveryPath}");

        try
        {
            // SynEdControl.Text groups removal and insertion into one native
            // undo action and preserves its own caret/selection state.
            control.Text = formatted;
            EnsureAvailable();
            if (!string.Equals(control.Text, formatted, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("TwinCAT's pane did not retain the exact formatted text.");
            }

            trace?.Invoke($"Verified pane replacement: {original.Length} -> {formatted.Length} characters.");
        }
        catch (Exception exception)
        {
            try
            {
                EnsureAvailable();
            }
            catch (Exception recoveryException)
            {
                throw new InvalidOperationException(
                    $"The pane changed during formatting; Undo was not applied to a different buffer. Original text: {recoveryPath}",
                    new AggregateException(exception, recoveryException));
            }

            if (string.Equals(control.Text, original, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The editor rejected formatting; the original text is unchanged.", exception);
            }

            try
            {
                undo.Invoke(control, null);
                if (!string.Equals(control.Text, original, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Undo did not restore the original pane text.");
                }
            }
            catch (Exception recoveryException)
            {
                throw new InvalidOperationException(
                    $"Formatting failed and automatic recovery could not be verified. Original text: {recoveryPath}",
                    new AggregateException(exception, recoveryException));
            }

            throw new InvalidOperationException("Formatting failed; the original pane text was restored with Undo.", exception);
        }
    }

    private void EnsureAvailable()
    {
        if (control.IsDisposed || !ReferenceEquals(buffer, GetProperty(control, "Buffer")))
        {
            throw new InvalidOperationException("The TwinCAT editor pane or its buffer changed.");
        }
    }

    private static string SaveOriginal(string original)
    {
        // Retain the exact pre-edit text independently of the editor's undo stack.
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "tc_format", "Recovery");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.st");
        File.WriteAllText(path, original);
        return path;
    }

    private static IEnumerable<Control> FindTextControls(Control root)
    {
        if (root.GetType().FullName == ControlTypeName)
        {
            yield return root;
            yield break;
        }

        foreach (Control child in root.Controls)
        {
            foreach (var textControl in FindTextControls(child))
            {
                yield return textControl;
            }
        }
    }

    private static object? GetProperty(object target, string name)
    {
        var type = target.GetType();
        var property = type.GetProperty(name);
        if (property is null)
        {
            property = type.GetInterfaces().Select(candidate => candidate.GetProperty(name))
                .FirstOrDefault(candidate => candidate is not null);
        }

        return property?.GetValue(target);
    }
}
