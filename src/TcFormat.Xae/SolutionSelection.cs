using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using EnvDTE;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace TcFormat.Xae;

internal static class SolutionSelection
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(
        [".git", ".vs", "_Boot", "_CompileInfo", "bin", "obj"],
        StringComparer.OrdinalIgnoreCase);

    public static async Task<IReadOnlyCollection<string>> GetTargetsAsync(AsyncPackage package)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();
        var dte = await package.GetServiceAsync(typeof(DTE)) as DTE
            ?? throw new InvalidOperationException("Visual Studio automation services are unavailable.");
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedItems = dte.SelectedItems;
        for (var index = 1; index <= selectedItems.Count; index++)
        {
            var selectedItem = selectedItems.Item(index);
            var target = ResolveSelectedItem(selectedItem);
            if (target is not null)
            {
                targets.Add(target);
            }
        }

        return targets;
    }

    public static async Task<IReadOnlyCollection<string>> GetDirtyDocumentsAsync(
        AsyncPackage package,
        IReadOnlyCollection<string> targets)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();
        var runningDocumentTable =
            await package.GetServiceAsync(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable4
            ?? throw new InvalidOperationException("Visual Studio running document table is unavailable.");
        var dirtyDocuments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in DiscoverFiles(targets))
        {
            if (runningDocumentTable.IsMonikerValid(filePath))
            {
                var documentCookie = runningDocumentTable.GetDocumentCookie(filePath);
                if (runningDocumentTable.IsDocumentDirty(documentCookie))
                {
                    dirtyDocuments.Add(filePath);
                }
            }
        }

        return dirtyDocuments;
    }

    private static string? ResolveSelectedItem(SelectedItem selectedItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            if (selectedItem.ProjectItem is ProjectItem projectItem)
            {
                return ResolveProjectItem(projectItem);
            }

            if (selectedItem.Project is Project project)
            {
                return ResolveProject(project);
            }
        }
        catch (COMException)
        {
        }

        return null;
    }

    private static string? ResolveProjectItem(ProjectItem projectItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ProjectItem? current = projectItem;
        for (var depth = 0; current is not null && depth < 32; depth++)
        {
            var target = GetProjectItemTarget(current);
            if (target is not null)
            {
                return target;
            }

            try
            {
                current = current.Collection?.Parent as ProjectItem;
            }
            catch (COMException)
            {
                current = null;
            }
        }

        return null;
    }

    private static string? GetProjectItemTarget(ProjectItem projectItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            for (short index = 1; index <= projectItem.FileCount; index++)
            {
                var path = projectItem.FileNames[index];
                if (Directory.Exists(path))
                {
                    return Path.GetFullPath(path);
                }

                if (File.Exists(path) && ActiveDocument.IsSupportedFile(path))
                {
                    return Path.GetFullPath(path);
                }

                var backingFilePath = ActiveDocument.GetBackingFilePath(path);
                if (!string.Equals(backingFilePath, path, StringComparison.Ordinal) &&
                    File.Exists(backingFilePath))
                {
                    return Path.GetFullPath(backingFilePath);
                }
            }
        }
        catch (COMException)
        {
        }

        return null;
    }

    private static string? ResolveProject(Project project)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            if (string.IsNullOrWhiteSpace(project.FullName))
            {
                return null;
            }

            var projectPath = Path.GetFullPath(project.FullName);
            return File.Exists(projectPath) ? Path.GetDirectoryName(projectPath) : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static IReadOnlyCollection<string> DiscoverFiles(IReadOnlyCollection<string> targets)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in targets)
        {
            if (File.Exists(target))
            {
                if (ActiveDocument.IsSupportedFile(target))
                {
                    files.Add(Path.GetFullPath(target));
                }

                continue;
            }

            if (Directory.Exists(target))
            {
                var pending = new Stack<string>();
                pending.Push(target);
                while (pending.Count > 0)
                {
                    var directory = pending.Pop();
                    foreach (var filePath in Directory.EnumerateFiles(directory))
                    {
                        if (ActiveDocument.IsSupportedFile(filePath))
                        {
                            files.Add(Path.GetFullPath(filePath));
                        }
                    }

                    foreach (var childDirectory in Directory.EnumerateDirectories(directory))
                    {
                        if (!ExcludedDirectoryNames.Contains(Path.GetFileName(childDirectory)))
                        {
                            pending.Push(childDirectory);
                        }
                    }
                }
            }
        }

        return files;
    }
}
