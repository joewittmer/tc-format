using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace TcFormat.Xae;

internal sealed class ActiveDocument
{
    private static readonly string[] SupportedExtensions =
    [
        ".st",
        ".iecst",
        ".tcpou",
        ".tcdut",
        ".tcgvl",
        ".tcitf",
        ".tcprg"
    ];

    private ActiveDocument(IVsWindowFrame frame, IVsTextView view, IVsTextLines? buffer, string filePath,
        string documentMoniker, TwinCatEditorPane? pane = null)
    {
        Frame = frame;
        View = view;
        Buffer = buffer;
        FilePath = filePath;
        DocumentMoniker = documentMoniker;
        Pane = pane;
    }

    private IVsTextView View { get; }

    private IVsWindowFrame Frame { get; }

    private IVsTextLines? Buffer { get; }

    private TwinCatEditorPane? Pane { get; }

    public string FilePath { get; }

    private string DocumentMoniker { get; }

    public static async Task<ActiveDocument> GetAsync(AsyncPackage package, Action<string>? trace = null)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();
        var selection = await package.GetServiceAsync(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection
            ?? throw new InvalidOperationException("Visual Studio selection service is unavailable.");
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();
        ErrorHandler.ThrowOnFailure(selection.GetCurrentElementValue(
            (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame,
            out var frameObject));
        var frame = frameObject as IVsWindowFrame
            ?? throw new InvalidOperationException("No active document frame is available.");
        ErrorHandler.ThrowOnFailure(frame.GetProperty(
            (int)__VSFPROPID.VSFPROPID_pszMkDocument,
            out var pathObject));
        var documentMoniker = pathObject as string
            ?? throw new InvalidOperationException("The active TwinCAT document has no backing file path.");
        trace?.Invoke($"Document: {documentMoniker}");
        var filePath = GetBackingFilePath(documentMoniker);

        // Resolve both the identity and text view from this frame. The global text
        // manager can retain a view belonging to a different TwinCAT document.
        ErrorHandler.ThrowOnFailure(frame.GetProperty(
            (int)__VSFPROPID.VSFPROPID_DocView,
            out var documentView));
        trace?.Invoke($"Document view: {documentView?.GetType().FullName ?? "null"}; " +
            $"text view={documentView is IVsTextView}; code window={documentView is IVsCodeWindow}");
        if (documentView is IVsTextView twinCatView && TwinCatEditorPane.TryCreate(documentView, trace) is { } pane)
        {
            return new ActiveDocument(frame, twinCatView, null, filePath, documentMoniker, pane);
        }

        IVsTextView? view;
        // TwinCAT's VSEditor implements both interfaces. Its direct text view
        // need not be the focused declaration/implementation pane.
        if (documentView is IVsCodeWindow codeWindow)
        {
            trace?.Invoke("Selecting the code window's last active pane.");
            ErrorHandler.ThrowOnFailure(codeWindow.GetLastActiveView(out view));
        }
        else
        {
            trace?.Invoke("Selecting the document's single text view.");
            view = documentView as IVsTextView;
        }

        if (view is null)
        {
            throw new InvalidOperationException(
                "TwinCAT did not expose a text view in the active document window. " +
                "Formatting cannot continue. See the tc_format Output pane for details.");
        }

        ErrorHandler.ThrowOnFailure(view.GetBuffer(out var buffer));
        trace?.Invoke($"Resolved editor window: 0x{view.GetWindowHandle().ToInt64():X}");

        return new ActiveDocument(frame, view, buffer, filePath, documentMoniker);
    }

    public bool IsSameEditor(ActiveDocument other) =>
        ReferenceEquals(Frame, other.Frame) &&
        ReferenceEquals(View, other.View) &&
        ReferenceEquals(Buffer, other.Buffer) &&
        (Pane is null ? other.Pane is null : other.Pane is not null && Pane.IsSamePane(other.Pane)) &&
        string.Equals(DocumentMoniker, other.DocumentMoniker, StringComparison.OrdinalIgnoreCase);

    public string GetText()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (Pane is not null)
        {
            return Pane.GetText();
        }

        var buffer = Buffer!;
        ErrorHandler.ThrowOnFailure(buffer.GetLastLineIndex(out var lastLine, out var lastIndex));
        ErrorHandler.ThrowOnFailure(buffer.GetLineText(0, 0, lastLine, lastIndex, out var text));
        return text;
    }

    public void ReplaceText(string originalText, string formattedText, Action<string>? trace = null)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (Pane is not null)
        {
            Pane.ReplaceText(originalText, formattedText, trace);
            return;
        }

        if (!string.Equals(GetText(), originalText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The editor changed before formatting could be applied.");
        }

        var view = View;
        var buffer = Buffer!;
        ErrorHandler.ThrowOnFailure(buffer.GetLastLineIndex(out var lastLine, out var lastIndex));
        ErrorHandler.ThrowOnFailure(view.GetCaretPos(out var caretLine, out var caretColumn));
        var compoundAction = view as IVsCompoundAction;
        var compoundActionOpened = compoundAction is not null &&
            ErrorHandler.Succeeded(compoundAction.OpenCompoundAction("Format Structured Text"));
        var compoundActionClosed = false;
        var textPointer = Marshal.StringToCoTaskMemUni(formattedText);
        try
        {
            trace?.Invoke("Replacing captured editor buffer.");
            ErrorHandler.ThrowOnFailure(buffer.ReplaceLines(
                0,
                0,
                lastLine,
                lastIndex,
                textPointer,
                formattedText.Length,
                null));

            if (!string.Equals(GetText(), formattedText, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The editor did not retain the exact formatted text.");
            }

            if (compoundActionOpened)
            {
                trace?.Invoke("Closing formatting undo action.");
                compoundActionClosed = ErrorHandler.Succeeded(compoundAction!.CloseCompoundAction());
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(textPointer);
            if (compoundActionOpened && !compoundActionClosed)
            {
                _ = compoundAction!.AbortCompoundAction();
            }
        }

        ErrorHandler.ThrowOnFailure(buffer.GetLastLineIndex(out var newLastLine, out _));
        var restoredLine = Math.Min(caretLine, newLastLine);
        ErrorHandler.ThrowOnFailure(buffer.GetLengthOfLine(restoredLine, out var restoredLineLength));
        var restoredColumn = Math.Min(caretColumn, restoredLineLength);
        trace?.Invoke($"Restoring caret: {restoredLine}, {restoredColumn}.");
        ErrorHandler.ThrowOnFailure(view.SetCaretPos(restoredLine, restoredColumn));
        trace?.Invoke("Editor replacement finished.");
    }

    public static bool IsSupportedFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        foreach (var supportedExtension in SupportedExtensions)
        {
            if (string.Equals(extension, supportedExtension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string GetBackingFilePath(string documentMoniker)
    {
        if (IsSupportedFile(documentMoniker))
        {
            return documentMoniker;
        }

        var virtualNodeSeparator = documentMoniker.IndexOf('@');
        while (virtualNodeSeparator > 0)
        {
            var candidate = documentMoniker.Substring(0, virtualNodeSeparator);
            if (IsSupportedFile(candidate))
            {
                return candidate;
            }

            virtualNodeSeparator = documentMoniker.IndexOf('@', virtualNodeSeparator + 1);
        }

        return documentMoniker;
    }

}
