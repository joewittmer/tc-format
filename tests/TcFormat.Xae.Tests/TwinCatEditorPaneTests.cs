using System.Windows.Forms;
using _3S.CoDeSys.Controls.Controls;
using TcFormat.Xae;
using Xunit;

namespace TcFormat.Xae.Tests;

public sealed class TwinCatEditorPaneTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EditsOnlyTheChosenPaneAndReadsCommentOnlyChanges(bool declarationActive)
    {
        using var editor = new TestEditor { InterfaceActive = declarationActive };
        var selected = declarationActive ? editor.Declaration : editor.Implementation;
        var other = declarationActive ? editor.Implementation : editor.Declaration;
        other.Text = "unrelated unsaved text";
        selected.Text = "Run();\n// comment";
        var pane = TwinCatEditorPane.FromEditor(editor);

        // A comment-only user edit must be read live, even after prior reads.
        Assert.Equal(selected.Text, pane.GetText());
        selected.Text = "Run();\n// changed comment";
        var original = pane.GetText();
        Assert.Contains("changed comment", original);
        pane.ReplaceText(original, "Run();\n\n// changed comment", null, Save);

        Assert.Equal("Run();\n\n// changed comment", selected.Text);
        Assert.Equal("unrelated unsaved text", other.Text);
        selected.Undo();
        Assert.Equal(original, selected.Text);
    }

    [Fact]
    public void NoOpDoesNotWriteOrCreateRecoveryCopy()
    {
        using var editor = new TestEditor();
        editor.Declaration.Text = "original";
        var pane = TwinCatEditorPane.FromEditor(editor);
        pane.ReplaceText("original", "original", null, _ => throw new InvalidOperationException("must not back up a no-op"));
        Assert.Equal(1, editor.Declaration.Writes);
    }

    [Fact]
    public void RefusesAnAmbiguousPane()
    {
        using var editor = new TestEditor();
        Assert.Throws<InvalidOperationException>(() => TwinCatEditorPane.FromEditor(new { editor.Control }));
    }

    [Fact]
    public void CorruptedWriteIsDetectedAndUndone()
    {
        using var editor = new TestEditor();
        editor.Declaration.Text = "original";
        editor.Declaration.CorruptWrite = true;
        var pane = TwinCatEditorPane.FromEditor(editor);
        var error = Assert.Throws<InvalidOperationException>(() => pane.ReplaceText("original", "formatted", null, Save));
        Assert.Contains("restored", error.Message);
        Assert.Equal("original", editor.Declaration.Text);
        Assert.Equal(1, editor.Declaration.Undos);
    }

    [Fact]
    public void IgnoredWriteDoesNotUndoPreviousUserEdits()
    {
        using var editor = new TestEditor();
        editor.Declaration.Text = "original";
        editor.Declaration.IgnoreWrite = true;
        var pane = TwinCatEditorPane.FromEditor(editor);
        var error = Assert.Throws<InvalidOperationException>(() => pane.ReplaceText("original", "formatted", null, Save));
        Assert.Contains("unchanged", error.Message);
        Assert.Equal(0, editor.Declaration.Undos);
    }

    [Fact]
    public void FailedRecoveryReportsBackupPath()
    {
        using var editor = new TestEditor();
        editor.Declaration.Text = "original";
        editor.Declaration.CorruptWrite = true;
        editor.Declaration.FailUndo = true;
        var pane = TwinCatEditorPane.FromEditor(editor);
        var error = Assert.Throws<InvalidOperationException>(() => pane.ReplaceText("original", "formatted", null, Save));
        Assert.Contains("test-original.st", error.Message);
    }

    [Fact]
    public void RejectsStaleTextAndUnmodifiableBuffers()
    {
        using var editor = new TestEditor();
        editor.Declaration.Text = "new user edit";
        var pane = TwinCatEditorPane.FromEditor(editor);
        Assert.Throws<InvalidOperationException>(() => pane.ReplaceText("old", "formatted", null, Save));
        editor.Declaration.Buffer.Modifiable = false;
        Assert.Throws<InvalidOperationException>(() => pane.ReplaceText("new user edit", "formatted", null, Save));
        Assert.Equal(1, editor.Declaration.Writes);
    }

    [Fact]
    public void DetectsPaneSwitchAndReplacedBuffer()
    {
        using var editor = new TestEditor();
        var first = TwinCatEditorPane.FromEditor(editor);
        editor.InterfaceActive = false;
        Assert.False(first.IsSamePane(TwinCatEditorPane.FromEditor(editor)));
        editor.Declaration.Buffer = new TestBuffer();
        Assert.Throws<InvalidOperationException>(() => first.GetText());
    }

    [Fact]
    public void DoesNotUndoIntoAReplacementBuffer()
    {
        using var editor = new TestEditor();
        editor.Declaration.Text = "original";
        editor.Declaration.SwapBufferOnWrite = true;
        var pane = TwinCatEditorPane.FromEditor(editor);
        var error = Assert.Throws<InvalidOperationException>(() => pane.ReplaceText("original", "formatted", null, Save));
        Assert.Contains("different buffer", error.Message);
        Assert.Equal(0, editor.Declaration.Undos);
    }

    [Fact]
    public void BackupFailurePreventsMutation()
    {
        using var editor = new TestEditor();
        editor.Declaration.Text = "original";
        var pane = TwinCatEditorPane.FromEditor(editor);
        Assert.Throws<IOException>(() => pane.ReplaceText("original", "formatted", null,
            _ => throw new IOException("backup failed")));
        Assert.Equal("original", editor.Declaration.Text);
        Assert.Equal(1, editor.Declaration.Writes);
    }

    private static string Save(string source) => "test-original.st";

    private sealed class TestEditor : IDisposable
    {
        public Control Control { get; } = new Panel();
        public SynEdControl Declaration { get; } = new();
        public SynEdControl Implementation { get; } = new();
        public bool InterfaceActive { get; set; } = true;
        public TestPane InterfaceEditorView => new(Declaration);
        public TestPane ImplementationEditorView => new(Implementation);

        public TestEditor()
        {
            Control.Controls.Add(Declaration);
            Control.Controls.Add(Implementation);
        }

        public void Dispose() => Control.Dispose();
    }

    private sealed record TestPane(Control Control);
}
