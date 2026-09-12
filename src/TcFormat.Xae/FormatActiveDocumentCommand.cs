using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace TcFormat.Xae;

internal sealed class FormatActiveDocumentCommand
{
    private const int CommandId = 0x0100;
    private const int ShortcutCommandIdBase = 0x0110;
    private const string OutputPaneTitle = "tc_format";
    private static readonly Guid CommandSet = new("18ccaad0-d88c-4f1c-b6de-eff263bea1b7");
    private static readonly Guid OutputPaneGuid = new("ca7d00af-540b-45cf-90b9-d11266d60841");
    private readonly TcFormatPackage package;
    private readonly FormatterProcess formatterProcess = new();

    private FormatActiveDocumentCommand(TcFormatPackage package, OleMenuCommandService commandService)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        this.package = package;
        var commandId = new CommandID(CommandSet, CommandId);
        commandService.AddCommand(new MenuCommand(Execute, commandId));
        RegisterShortcutCommands(commandService);
    }

    public static async Task InitializeAsync(TcFormatPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        if (commandService is not null)
        {
            _ = new FormatActiveDocumentCommand(package, commandService);
        }
    }

    private void Execute(object sender, EventArgs eventArgs)
    {
        _ = package.JoinableTaskFactory.RunAsync(() => ExecuteAsync("FormatActiveDocument"));
    }

    private void RegisterShortcutCommands(OleMenuCommandService commandService)
    {
        foreach (FormatShortcutPreset shortcut in Enum.GetValues(typeof(FormatShortcutPreset)))
        {
            var commandId = new CommandID(CommandSet, ShortcutCommandIdBase + (int)shortcut);
            var command = new OleMenuCommand(
                (_, _) =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    ExecuteShortcut(shortcut);
                },
                commandId);
            command.BeforeQueryStatus += (_, _) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var isSelected = package.GetGeneralOptions().FormatShortcut == shortcut;
                command.Enabled = isSelected;
                command.Supported = isSelected;
            };
            commandService.AddCommand(command);
        }
    }

    private void ExecuteShortcut(FormatShortcutPreset shortcut)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (package.GetGeneralOptions().FormatShortcut == shortcut)
        {
            _ = package.JoinableTaskFactory.RunAsync(() => ExecuteAsync($"Shortcut: {shortcut}"));
        }
    }

    private async Task ExecuteAsync(string trigger)
    {
        var trace = new List<string>();
        void Record(string message) => trace.Add($"{DateTimeOffset.Now:O} {message}");
        Record($"tc_format XAE {typeof(TcFormatPackage).Assembly.GetName().Version}; {trigger}");
        try
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            Record("Capturing active editor.");
            var activeDocument = await ActiveDocument.GetAsync(package, Record);
            if (!ActiveDocument.IsSupportedFile(activeDocument.FilePath))
            {
                await ReportErrorAsync("The active editor is not backed by a supported TwinCAT Structured Text file.");
                return;
            }

            var originalText = activeDocument.GetText();
            Record($"Captured {originalText.Length} characters; starting formatter.");
            var result = await formatterProcess.FormatAsync(
                originalText,
                activeDocument.FilePath,
                CancellationToken.None);
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!result.Succeeded)
            {
                Record($"Formatter failed: {result.Error}");
                await ReportErrorAsync(result.Error);
                return;
            }

            Record("Checking active editor before applying result.");
            var currentDocument = await ActiveDocument.GetAsync(package, Record);
            if (!activeDocument.IsSameEditor(currentDocument) || !string.Equals(
                    originalText,
                    currentDocument.GetText(),
                    StringComparison.Ordinal))
            {
                Record("Discarded result: editor identity or text changed.");
                await ReportErrorAsync("The active editor or its text changed while tc_format was running. No formatter changes were applied.");
                return;
            }

            if (string.Equals(originalText, result.FormattedText, StringComparison.Ordinal))
            {
                Record("Already formatted; no editor write.");
                await WriteOutputAsync("Active Structured Text editor is already formatted.");
                return;
            }

            activeDocument.ReplaceText(originalText, result.FormattedText, Record);
            Record("Checking active editor after replacement.");
            var finalDocument = await ActiveDocument.GetAsync(package, Record);
            if (!activeDocument.IsSameEditor(finalDocument))
            {
                Record("Editor changed during replacement.");
                await ReportErrorAsync("XAE changed the active editor during formatting. " +
                    "See the tc_format Output pane for the editor trace.");
                return;
            }

            await WriteOutputAsync("Formatted the active Structured Text editor. The document remains unsaved.");
        }
        catch (Exception exception)
        {
            Record($"Error: {exception}");
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            await ReportErrorAsync(exception.Message);
        }
        finally
        {
            var diagnostics = string.Join(Environment.NewLine, trace);
            try
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "tc_format", "Logs");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "last-editor-format.log"), diagnostics);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics += $"{Environment.NewLine}Could not save editor trace: {exception.Message}";
            }

            await WriteOutputAsync(diagnostics);
        }
    }

    private async Task ReportErrorAsync(string message)
    {
        await WriteOutputAsync($"error: {message}");
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();
        VsShellUtilities.ShowMessageBox(
            package,
            message,
            "tc_format",
            OLEMSGICON.OLEMSGICON_CRITICAL,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }

    private async Task WriteOutputAsync(string message)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();
        var outputWindow = await package.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
        if (outputWindow is null)
        {
            return;
        }

        var paneGuid = OutputPaneGuid;
        _ = outputWindow.CreatePane(ref paneGuid, OutputPaneTitle, 1, 0);
        if (ErrorHandler.Succeeded(outputWindow.GetPane(ref paneGuid, out var pane)) && pane is not null)
        {
            pane.OutputStringThreadSafe($"{message}{Environment.NewLine}");
        }
    }
}
