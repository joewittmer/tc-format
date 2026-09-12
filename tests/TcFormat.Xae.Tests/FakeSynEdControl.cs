using System.ComponentModel;
using System.Windows.Forms;

// Match the public editor-control contract used by the compatibility adapter.
namespace _3S.CoDeSys.Controls.Controls;

public sealed class SynEdControl : Control
{
    private string text = string.Empty;
    private string previous = string.Empty;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TestBuffer Buffer { get; set; } = new();
    [DefaultValue(false)]
    public bool CorruptWrite { get; set; }
    [DefaultValue(false)]
    public bool IgnoreWrite { get; set; }
    [DefaultValue(false)]
    public bool FailUndo { get; set; }
    [DefaultValue(false)]
    public bool SwapBufferOnWrite { get; set; }
    public int Writes { get; private set; }
    public int Undos { get; private set; }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => text;
        set
        {
            Writes++;
            if (IgnoreWrite)
            {
                return;
            }

            previous = text;
            text = (value ?? string.Empty) + (CorruptWrite ? "TRASH" : string.Empty);
            if (SwapBufferOnWrite)
            {
                Buffer = new TestBuffer();
            }
        }
    }

    public void Undo()
    {
        Undos++;
        if (!FailUndo)
        {
            text = previous;
        }
    }
}

public sealed class TestBuffer
{
    public bool Modifiable { get; set; } = true;
}
