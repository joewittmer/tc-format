using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace TcFormat.Xae;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("tc_format for TwinCAT XAE", "Formats active TwinCAT Structured Text editors.", "0.1")]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideOptionPage(typeof(GeneralOptionsPage), "tc_format", "General", 0, 0, true)]
[Guid(PackageGuidString)]
public sealed class TcFormatPackage : AsyncPackage
{
    public const string PackageGuidString = "7c56c40e-00aa-42dc-b3ad-d8918feb83e1";

    private FormatOnSaveController? formatOnSaveController;

    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        await FormatActiveDocumentCommand.InitializeAsync(this);
        await FormatSolutionSelectionCommand.InitializeAsync(this);
        formatOnSaveController = await FormatOnSaveController.CreateAsync(this);
    }

    internal GeneralOptionsPage GetGeneralOptions()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return (GeneralOptionsPage)GetDialogPage(typeof(GeneralOptionsPage));
    }

    protected override void Dispose(bool disposing)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (disposing)
        {
            formatOnSaveController?.Dispose();
            formatOnSaveController = null;
        }

        base.Dispose(disposing);
    }
}
