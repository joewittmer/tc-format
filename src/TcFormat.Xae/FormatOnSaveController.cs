using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace TcFormat.Xae;

internal sealed class FormatOnSaveController : IVsRunningDocTableEvents3, IDisposable
{
    private readonly TcFormatPackage package;
    private readonly IVsRunningDocumentTable runningDocumentTable;
    private readonly IVsRunningDocumentTable4 runningDocumentTable4;
    private readonly FormatterProcess formatterProcess = new();
    private uint eventsCookie;

    private FormatOnSaveController(
        TcFormatPackage package,
        IVsRunningDocumentTable runningDocumentTable,
        IVsRunningDocumentTable4 runningDocumentTable4)
    {
        this.package = package;
        this.runningDocumentTable = runningDocumentTable;
        this.runningDocumentTable4 = runningDocumentTable4;
    }

    public static async Task<FormatOnSaveController> CreateAsync(TcFormatPackage package)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();
        var runningDocumentTable =
            await package.GetServiceAsync(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable
            ?? throw new InvalidOperationException("Visual Studio running document table is unavailable.");
        var runningDocumentTable4 = runningDocumentTable as IVsRunningDocumentTable4
            ?? throw new InvalidOperationException("Visual Studio running document table v4 is unavailable.");
        var controller = new FormatOnSaveController(
            package,
            runningDocumentTable,
            runningDocumentTable4);
        ErrorHandler.ThrowOnFailure(runningDocumentTable.AdviseRunningDocTableEvents(
            controller,
            out controller.eventsCookie));
        return controller;
    }

    public int OnBeforeSave(uint docCookie)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (!package.GetGeneralOptions().FormatOnSave)
        {
            return VSConstants.S_OK;
        }

        try
        {
            var savingFilePath = runningDocumentTable4.GetDocumentMoniker(docCookie);
            if (!ActiveDocument.IsSupportedFile(savingFilePath))
            {
                return VSConstants.S_OK;
            }

            var activeDocument = package.JoinableTaskFactory.Run(() => ActiveDocument.GetAsync(package));
            if (!string.Equals(
                    Path.GetFullPath(savingFilePath),
                    Path.GetFullPath(activeDocument.FilePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return VSConstants.S_OK;
            }

            var originalText = ActiveDocument.GetText(activeDocument.View);
            var result = package.JoinableTaskFactory.Run(
                () => formatterProcess.FormatAsync(
                    originalText,
                    activeDocument.FilePath,
                    CancellationToken.None));
            if (!result.Succeeded)
            {
                WriteOutput($"format-on-save error: {result.Error}");
                return VSConstants.S_OK;
            }

            if (!string.Equals(originalText, result.FormattedText, StringComparison.Ordinal))
            {
                ActiveDocument.ReplaceText(activeDocument.View, result.FormattedText);
                WriteOutput("Formatted the active Structured Text editor before saving.");
            }
        }
        catch (Exception exception)
        {
            WriteOutput($"format-on-save error: {exception.Message}");
        }

        return VSConstants.S_OK;
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (eventsCookie != 0)
        {
            _ = runningDocumentTable.UnadviseRunningDocTableEvents(eventsCookie);
            eventsCookie = 0;
        }
    }

    public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => VSConstants.S_OK;

    public int OnAfterAttributeChangeEx(
        uint docCookie,
        uint grfAttribs,
        IVsHierarchy hierarchyOld,
        uint itemidOld,
        string monikerOld,
        IVsHierarchy hierarchyNew,
        uint itemidNew,
        string monikerNew) => VSConstants.S_OK;

    public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame frame) => VSConstants.S_OK;

    public int OnAfterFirstDocumentLock(
        uint docCookie,
        uint lockType,
        uint readLocksRemaining,
        uint editLocksRemaining) => VSConstants.S_OK;

    public int OnAfterSave(uint docCookie) => VSConstants.S_OK;

    public int OnBeforeDocumentWindowShow(
        uint docCookie,
        int firstShow,
        IVsWindowFrame frame) => VSConstants.S_OK;

    public int OnBeforeLastDocumentUnlock(
        uint docCookie,
        uint lockType,
        uint readLocksRemaining,
        uint editLocksRemaining) => VSConstants.S_OK;

    private static void WriteOutput(string message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
        if (outputWindow is null)
        {
            return;
        }

        var paneGuid = new Guid("ca7d00af-540b-45cf-90b9-d11266d60841");
        _ = outputWindow.CreatePane(ref paneGuid, "tc_format", 1, 0);
        if (ErrorHandler.Succeeded(outputWindow.GetPane(ref paneGuid, out var pane)) && pane is not null)
        {
            pane.OutputStringThreadSafe($"{message}{Environment.NewLine}");
        }
    }
}
