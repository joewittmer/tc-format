using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace TcFormat.Xae;

public sealed class GeneralOptionsPage : DialogPage
{
    [Category("Formatting")]
    [DisplayName("Format on save")]
    [Description("Format the active TwinCAT Structured Text editor immediately before it is saved.")]
    [DefaultValue(false)]
    public bool FormatOnSave { get; set; }

    [Category("Keyboard")]
    [DisplayName("Format shortcut")]
    [Description("Keyboard shortcut for formatting the active TwinCAT Structured Text document.")]
    [DefaultValue(FormatShortcutPreset.CtrlRThenCtrlF)]
    public FormatShortcutPreset FormatShortcut { get; set; } = FormatShortcutPreset.CtrlRThenCtrlF;
}
