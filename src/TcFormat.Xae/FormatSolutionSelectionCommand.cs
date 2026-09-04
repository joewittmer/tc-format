using System;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace TcFormat.Xae;

internal sealed class FormatSolutionSelectionCommand
{
    private const int CommandId = 0x0101;
    private const string OutputPaneTitle = "tc_format";
    private static readonly Guid CommandSet = new("18ccaad0-d88c-4f1c-b6de-eff263bea1b7");
    private static readonly Guid OutputPaneGuid = new("ca7d00af-540b-45cf-90b9-d11266d60841");
    private readonly AsyncPackage package;
    private readonly FormatterProcess formatterProcess = new();

    private FormatSolutionSelectionCommand(AsyncPackage package, OleMenuCommandService commandService)
    {
        this.package = package;
        commandService.AddCommand(new MenuCommand(Execute, new CommandID(CommandSet, CommandId)));
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();
        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        if (commandService is not null)
        {
            _ = new FormatSolutionSelectionCommand(package, commandService);
        }
    }

    private void Execute(object sender, EventArgs eventArgs)
    {
        _ = package.JoinableTaskFactory.RunAsync(ExecuteAsync);
    }

    private async Task ExecuteAsync()
    {
        try
        {
            var targets = await SolutionSelection.GetTargetsAsync(package);
            if (targets.Count == 0)
            {
                await ReportErrorAsync(
                    "The selected Solution Explorer item does not resolve to a supported " +
                    "Structured Text file, folder, or project.");
                return;
            }

            var dirtyDocuments = await SolutionSelection.GetDirtyDocumentsAsync(package, targets);
            if (dirtyDocuments.Count > 0)
            {
                var paths = string.Join(Environment.NewLine, dirtyDocuments.Select(path => $"  {path}"));
                await ReportErrorAsync(
                    "Formatting was canceled because the selection contains unsaved Structured Text documents:" +
                    Environment.NewLine + Environment.NewLine + paths + Environment.NewLine + Environment.NewLine +
                    "Save or revert those documents, then run the command again.");
                return;
            }

            var result = await formatterProcess.FormatPathsAsync(targets, CancellationToken.None);
            if (!result.Succeeded)
            {
                await ReportErrorAsync(result.Error);
                return;
            }

            var output = string.IsNullOrWhiteSpace(result.FormattedText)
                ? "Formatting completed."
                : result.FormattedText.TrimEnd();
            await WriteOutputAsync(output);
        }
        catch (Exception exception)
        {
            await ReportErrorAsync(exception.Message);
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
