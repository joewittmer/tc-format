using System;
using System.ComponentModel.Design;
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
        _ = package.JoinableTaskFactory.RunAsync(ExecuteAsync);
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
            _ = package.JoinableTaskFactory.RunAsync(ExecuteAsync);
        }
    }

    private async Task ExecuteAsync()
    {
        try
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            var activeDocument = await ActiveDocument.GetAsync(package);
            if (!ActiveDocument.IsSupportedFile(activeDocument.FilePath))
            {
                await ReportErrorAsync("The active editor is not backed by a supported TwinCAT Structured Text file.");
                return;
            }

            var twinCatCodeItem = await TwinCatCodeItem.TryGetAsync(package, activeDocument);
            if (twinCatCodeItem is not null)
            {
                await FormatTwinCatCodeItemAsync(twinCatCodeItem, activeDocument.FilePath);
                return;
            }

            var originalText = ActiveDocument.GetText(activeDocument.View);
            var result = await formatterProcess.FormatAsync(
                originalText,
                activeDocument.FilePath,
                CancellationToken.None);
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!result.Succeeded)
            {
                await ReportErrorAsync(result.Error);
                return;
            }

            if (!string.Equals(
                    originalText,
                    ActiveDocument.GetText(activeDocument.View),
                    StringComparison.Ordinal))
            {
                await ReportErrorAsync("The editor changed while tc_format was running. No formatter changes were applied.");
                return;
            }

            if (string.Equals(originalText, result.FormattedText, StringComparison.Ordinal))
            {
                await WriteOutputAsync("Active Structured Text editor is already formatted.");
                return;
            }

            ActiveDocument.ReplaceText(activeDocument.View, result.FormattedText);
            await WriteOutputAsync("Formatted the active Structured Text editor. The document remains unsaved.");
        }
        catch (Exception exception)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            await ReportErrorAsync(exception.Message);
        }
    }

    private async Task FormatTwinCatCodeItemAsync(TwinCatCodeItem codeItem, string backingFilePath)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();
        var original = codeItem.Read();
        var declarationResult = await FormatSectionAsync(original.Declaration, backingFilePath);
        var implementationResult = await FormatSectionAsync(original.Implementation, backingFilePath);
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (!declarationResult.Succeeded)
        {
            await ReportErrorAsync(declarationResult.Error);
            return;
        }

        if (!implementationResult.Succeeded)
        {
            await ReportErrorAsync(implementationResult.Error);
            return;
        }

        var formatted = new TwinCatCodeSnapshot(
            original.Declaration is null ? null : declarationResult.FormattedText,
            original.Implementation is null ? null : implementationResult.FormattedText);
        var changedSections = codeItem.TryApply(original, formatted);
        if (changedSections < 0)
        {
            await ReportErrorAsync(
                "The TwinCAT editor changed while tc_format was running. No formatter changes were applied.");
            return;
        }

        if (changedSections == 0)
        {
            await WriteOutputAsync("Active TwinCAT Structured Text item is already formatted.");
            return;
        }

        await WriteOutputAsync(
            $"Formatted {changedSections} section{(changedSections == 1 ? string.Empty : "s")} " +
            "of the active TwinCAT Structured Text item. The document remains unsaved.");
    }

    private Task<FormatterProcessResult> FormatSectionAsync(string? source, string backingFilePath) =>
        source is null
            ? Task.FromResult(FormatterProcessResult.Success(string.Empty))
            : formatterProcess.FormatAsync(source, backingFilePath, CancellationToken.None);

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
