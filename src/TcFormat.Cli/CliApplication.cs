using System.Reflection;

using TcFormat.Core;
using TcFormat.Formatting;
using TcFormat.TwinCat;

namespace TcFormat.Cli;

internal static class CliApplication
{
    private const string HelpText =
        """
        tc_format - an opinionated Structured Text formatter for TwinCAT

        Usage:
          tc_format --help
          tc_format --version
          tc_format --stdin-filepath FILE
          tc_format [--check] FILE|DIRECTORY ...

        Editor integration:
          --stdin-filepath FILE  Read Structured Text from stdin and write the formatted text to stdout.
                                 FILE is used only to resolve .editorconfig settings.

        Exit codes:
          0  Formatting succeeded, or --check found no changes
          1  --check found files that would change
          2  Invalid input, configuration, or source text
        """;

    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            output.WriteLine(HelpText);
            return 0;
        }

        if (args is ["--version"])
        {
            var version = typeof(CliApplication).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "unknown";
            output.WriteLine($"tc_format {version}");
            return 0;
        }

        if (args.Length > 0 && args[0] == "--stdin-filepath")
        {
            return FormatStandardInput(args, input, output, error);
        }

        return FormatFiles(args, output, error);
    }

    private static int FormatStandardInput(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error)
    {
        if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            error.WriteLine("error: --stdin-filepath requires exactly one file path. Use --help for usage.");
            return 2;
        }

        try
        {
            var filePath = Path.GetFullPath(args[1]);
            var configuration = new EditorConfigResolver().Resolve(filePath);
            if (!configuration.IsValid)
            {
                WriteDiagnostics(configuration.Diagnostics.Select(diagnostic => diagnostic.ToString()), error);
                return 2;
            }

            var source = input.ReadToEnd();
            var result = StructuredTextFormatter.Format(source, configuration.Options);
            if (!result.IsValid)
            {
                WriteDiagnostics(result.Diagnostics.Select(diagnostic => $"{filePath}: {diagnostic}"), error);
                return 2;
            }

            output.Write(result.FormattedText);
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error.WriteLine($"error: {exception.Message}");
            return 2;
        }
    }

    private static int FormatFiles(string[] args, TextWriter output, TextWriter error)
    {
        var checkOnly = args.Length > 0 && args[0] == "--check";
        var paths = checkOnly ? args[1..] : args;
        if (paths.Length == 0 || paths.Any(path => path.StartsWith('-')))
        {
            error.WriteLine("error: provide at least one source file or directory. Use --help for usage.");
            return 2;
        }

        foreach (var path in paths)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                error.WriteLine($"error: {Path.GetFullPath(path)}: path does not exist.");
                return 2;
            }
        }

        var files = SourceFileDiscovery.Discover(paths);
        if (files.Count == 0)
        {
            output.WriteLine("No supported Structured Text files found.");
            return 0;
        }

        var resolver = new EditorConfigResolver();
        var staged = new List<StagedFile>();
        var diagnostics = new List<string>();

        foreach (var file in files)
        {
            try
            {
                var configuration = resolver.Resolve(file);
                diagnostics.AddRange(configuration.Diagnostics.Select(diagnostic => diagnostic.ToString()));
                if (!configuration.IsValid)
                {
                    continue;
                }

                var document = SourceDocumentLoader.Load(file);
                var replacements = new List<string>(document.Regions.Count);
                var fileIsValid = true;

                foreach (var region in document.Regions)
                {
                    var result = StructuredTextFormatter.Format(document.GetRegionText(region), configuration.Options);
                    if (!result.IsValid)
                    {
                        diagnostics.AddRange(result.Diagnostics.Select(diagnostic => $"{file}: {diagnostic}"));
                        fileIsValid = false;
                    }

                    replacements.Add(result.FormattedText);
                }

                if (fileIsValid)
                {
                    var updated = document.ReplaceRegions(replacements);
                    staged.Add(new StagedFile(document, updated));
                }
            }
            catch (SourceDocumentException exception)
            {
                diagnostics.Add(exception.Message);
            }
        }

        if (diagnostics.Count > 0)
        {
            WriteDiagnostics(diagnostics, error);
            error.WriteLine("No files were written because one or more errors occurred.");
            return 2;
        }

        var changed = staged.Where(file => file.Changed).ToArray();
        if (checkOnly)
        {
            foreach (var file in changed)
            {
                output.WriteLine($"Would reformat {file.Original.Path}");
            }

            output.WriteLine(
                changed.Length == 0
                    ? "All files are already formatted."
                    : $"{changed.Length} file(s) would be reformatted.");
            return changed.Length == 0 ? 0 : 1;
        }

        foreach (var file in changed)
        {
            AtomicSourceFileWriter.Write(file.Updated);
            output.WriteLine($"Reformatted {file.Original.Path}");
        }

        output.WriteLine(changed.Length == 0 ? "All files are already formatted." : $"Reformatted {changed.Length} file(s).");
        return 0;
    }

    private static void WriteDiagnostics(IEnumerable<string> diagnostics, TextWriter error)
    {
        foreach (var diagnostic in diagnostics)
        {
            error.WriteLine($"error: {diagnostic}");
        }
    }

    private sealed record StagedFile(SourceDocument Original, SourceDocument Updated)
    {
        public bool Changed => !string.Equals(Original.Text, Updated.Text, StringComparison.Ordinal);
    }
}
