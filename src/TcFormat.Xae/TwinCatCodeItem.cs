using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using EnvDTE;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.VisualStudio.Shell;

namespace TcFormat.Xae;

internal sealed class TwinCatCodeItem
{
    private const int MaximumSearchDepth = 16;
    private readonly object item;

    private TwinCatCodeItem(object item)
    {
        this.item = item;
    }

    public static async Task<TwinCatCodeItem?> TryGetAsync(
        AsyncPackage package,
        ActiveDocument activeDocument)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();
        var dte = await package.GetServiceAsync(typeof(DTE)) as DTE;
        var root = dte?.Solution.FindProjectItem(activeDocument.FilePath)?.Object;
        if (root is null)
        {
            return null;
        }

        var selectedItem = root;
        foreach (var nodeName in ActiveDocument.GetVirtualNodePath(activeDocument.DocumentMoniker))
        {
            selectedItem = FindDescendant(selectedItem, nodeName, 0);
            if (selectedItem is null)
            {
                return null;
            }
        }

        var codeItem = new TwinCatCodeItem(selectedItem);
        return codeItem.Read().HasCode ? codeItem : null;
    }

    public TwinCatCodeSnapshot Read()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return new TwinCatCodeSnapshot(
            TryReadText(item, "DeclarationText"),
            TryReadText(item, "ImplementationText"));
    }

    public int TryApply(TwinCatCodeSnapshot expected, TwinCatCodeSnapshot formatted)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (!Read().Equals(expected))
        {
            return -1;
        }

        var changedSections = 0;
        if (expected.Declaration is not null &&
            formatted.Declaration is not null &&
            !string.Equals(expected.Declaration, formatted.Declaration, StringComparison.Ordinal))
        {
            WriteText(item, "DeclarationText", formatted.Declaration);
            changedSections++;
        }

        if (expected.Implementation is not null &&
            formatted.Implementation is not null &&
            !string.Equals(expected.Implementation, formatted.Implementation, StringComparison.Ordinal))
        {
            WriteText(item, "ImplementationText", formatted.Implementation);
            changedSections++;
        }

        return changedSections;
    }

    private static object? FindDescendant(object parent, string name, int depth)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (depth >= MaximumSearchDepth || parent is not IEnumerable children)
        {
            return null;
        }

        foreach (var child in children)
        {
            if (child is null)
            {
                continue;
            }

            if (string.Equals(TryReadText(child, "Name"), name, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            var descendant = FindDescendant(child, name, depth + 1);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static string? TryReadText(object target, string propertyName)
    {
        try
        {
            dynamic dynamicTarget = target;
            return propertyName switch
            {
                "DeclarationText" => dynamicTarget.DeclarationText as string,
                "ImplementationText" => dynamicTarget.ImplementationText as string,
                "Name" => dynamicTarget.Name as string,
                _ => null
            };
        }
        catch (Exception exception) when (
            exception is COMException or RuntimeBinderException)
        {
            return null;
        }
    }

    private static void WriteText(object target, string propertyName, string value)
    {
        try
        {
            dynamic dynamicTarget = target;
            if (propertyName == "DeclarationText")
            {
                dynamicTarget.DeclarationText = value;
            }
            else
            {
                dynamicTarget.ImplementationText = value;
            }
        }
        catch (Exception exception) when (
            exception is COMException or RuntimeBinderException)
        {
            throw new InvalidOperationException(
                $"TwinCAT did not allow tc_format to update {propertyName}.",
                exception);
        }
    }
}

internal sealed class TwinCatCodeSnapshot : IEquatable<TwinCatCodeSnapshot>
{
    public TwinCatCodeSnapshot(string? declaration, string? implementation)
    {
        Declaration = declaration;
        Implementation = implementation;
    }

    public string? Declaration { get; }

    public string? Implementation { get; }

    public bool HasCode => Declaration is not null || Implementation is not null;

    public bool Equals(TwinCatCodeSnapshot? other) =>
        other is not null &&
        string.Equals(Declaration, other.Declaration, StringComparison.Ordinal) &&
        string.Equals(Implementation, other.Implementation, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TwinCatCodeSnapshot);

    public override int GetHashCode() => (Declaration, Implementation).GetHashCode();
}
