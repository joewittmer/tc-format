namespace TcFormat.TwinCat;

public static class SourceFileDiscovery
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(
        [".git", ".vs", "_Boot", "_CompileInfo", "bin", "obj"],
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Discover(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var fullPath = Path.GetFullPath(path);

            if (File.Exists(fullPath))
            {
                if (SourceFileTypes.IsSupported(fullPath))
                {
                    discovered.Add(fullPath);
                }

                continue;
            }

            if (Directory.Exists(fullPath))
            {
                DiscoverDirectory(fullPath, discovered);
            }
        }

        return discovered.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void DiscoverDirectory(string directory, ISet<string> discovered)
    {
        var pending = new Stack<string>();
        pending.Push(directory);

        while (pending.TryPop(out var current))
        {
            foreach (var file in Directory.EnumerateFiles(current))
            {
                if (SourceFileTypes.IsSupported(file))
                {
                    discovered.Add(Path.GetFullPath(file));
                }
            }

            foreach (var child in Directory.EnumerateDirectories(current))
            {
                if (!ExcludedDirectoryNames.Contains(Path.GetFileName(child)))
                {
                    pending.Push(child);
                }
            }
        }
    }
}

