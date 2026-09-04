using System;
using System.Collections.Generic;
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

    private ActiveDocument(IVsTextView view, string filePath, string documentMoniker)
    {
        View = view;
        FilePath = filePath;
        DocumentMoniker = documentMoniker;
    }

    public IVsTextView View { get; }

    public string FilePath { get; }

    public string DocumentMoniker { get; }

    public static async Task<ActiveDocument> GetAsync(AsyncPackage package)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();
        var textManager = await package.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager
            ?? throw new InvalidOperationException("Visual Studio text manager is unavailable.");
        ErrorHandler.ThrowOnFailure(textManager.GetActiveView(1, null, out var view));
        if (view is null)
        {
            throw new InvalidOperationException(
                "TwinCAT did not expose the active editor as a Visual Studio text view.");
        }

        var selection = await package.GetServiceAsync(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection
            ?? throw new InvalidOperationException("Visual Studio selection service is unavailable.");
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
        var filePath = GetBackingFilePath(documentMoniker);

        return new ActiveDocument(view, filePath, documentMoniker);
    }

    public static string GetText(IVsTextView view)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ErrorHandler.ThrowOnFailure(view.GetBuffer(out var buffer));
        ErrorHandler.ThrowOnFailure(buffer.GetLastLineIndex(out var lastLine, out var lastIndex));
        ErrorHandler.ThrowOnFailure(buffer.GetLineText(0, 0, lastLine, lastIndex, out var text));
        return text;
    }

    public static void ReplaceText(IVsTextView view, string formattedText)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ErrorHandler.ThrowOnFailure(view.GetBuffer(out var buffer));
        ErrorHandler.ThrowOnFailure(buffer.GetLastLineIndex(out var lastLine, out var lastIndex));
        ErrorHandler.ThrowOnFailure(view.GetCaretPos(out var caretLine, out var caretColumn));
        var compoundAction = view as IVsCompoundAction;
        var compoundActionOpened = compoundAction is not null &&
            ErrorHandler.Succeeded(compoundAction.OpenCompoundAction("Format Structured Text"));
        var compoundActionClosed = false;
        var textPointer = Marshal.StringToCoTaskMemUni(formattedText);
        try
        {
            ErrorHandler.ThrowOnFailure(buffer.ReplaceLines(
                0,
                0,
                lastLine,
                lastIndex,
                textPointer,
                formattedText.Length,
                null));

            if (compoundActionOpened)
            {
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
        ErrorHandler.ThrowOnFailure(view.SetCaretPos(restoredLine, restoredColumn));
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

    public static IReadOnlyList<string> GetVirtualNodePath(string documentMoniker)
    {
        var backingFilePath = GetBackingFilePath(documentMoniker);
        if (documentMoniker.Length <= backingFilePath.Length ||
            documentMoniker[backingFilePath.Length] != '@')
        {
            return Array.Empty<string>();
        }

        var virtualPath = documentMoniker.Substring(backingFilePath.Length + 1);
        var encodedSegments = virtualPath.Split(new[] { '@' }, StringSplitOptions.RemoveEmptyEntries);
        var segments = new List<string>(encodedSegments.Length);
        foreach (var encodedSegment in encodedSegments)
        {
            segments.Add(Uri.UnescapeDataString(encodedSegment));
        }

        return segments;
    }
}
