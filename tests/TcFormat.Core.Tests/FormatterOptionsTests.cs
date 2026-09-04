using TcFormat.Core;

using Xunit;

namespace TcFormat.Core.Tests;

public sealed class FormatterOptionsTests
{
    [Fact]
    public void DefaultsAreValid()
    {
        Assert.Empty(FormatterOptions.Default.Validate());
    }

    [Fact]
    public void InvalidNumericAndEnumValuesAreReported()
    {
        var options = FormatterOptions.Default with
        {
            KeywordCase = (KeywordCase)999,
            Indentation = FormatterOptions.Default.Indentation with { Size = 0 },
            Layout = FormatterOptions.Default.Layout with { MaximumConsecutiveBlankLines = -1 },
            BlankLines = FormatterOptions.Default.BlankLines with
            {
                BeforeCaseLabel = (BlankLinePolicy)999
            }
        };

        var errors = options.Validate();

        Assert.Contains(errors, error => error.Contains(nameof(FormatterOptions.KeywordCase), StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains(nameof(IndentationOptions.Size), StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains(nameof(LayoutOptions.MaximumConsecutiveBlankLines), StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains(nameof(BlankLineOptions.BeforeCaseLabel), StringComparison.Ordinal));
    }

    [Fact]
    public void CompleteExampleExplicitlySetsEverySupportedOption()
    {
        var editorConfigPath = Path.Combine(AppContext.BaseDirectory, "CompleteExample.editorconfig");
        var configuredValues = ReadStructuredTextSection(editorConfigPath);

        Assert.Equal(EditorConfigOptionCatalog.BuiltInValues.Count, configuredValues.Count);
        Assert.Empty(EditorConfigOptionCatalog.BuiltInValues.Keys.Except(configuredValues.Keys, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(configuredValues.Keys.Except(EditorConfigOptionCatalog.BuiltInValues.Keys, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void CompleteExampleUsesOpinionatedProfile()
    {
        var editorConfigPath = Path.Combine(AppContext.BaseDirectory, "CompleteExample.editorconfig");
        var configuredValues = ReadStructuredTextSection(editorConfigPath);

        Assert.Equal("space", configuredValues["indent_style"]);
        Assert.Equal("off", configuredValues["max_line_length"]);
        Assert.Equal("true", configuredValues["tc_format_align_end_of_line_comments"]);
        Assert.Equal("hanging", configuredValues["tc_format_wrap_calls"]);
        Assert.Equal("preserve", configuredValues["tc_format_wrap_initializers"]);
        Assert.Equal("preserve", configuredValues["tc_format_wrap_binary_expressions"]);
        Assert.Equal("after", configuredValues["tc_format_binary_operator_position"]);
    }

    [Fact]
    public void ConfigurationGuideProfileMatchesCompleteExample()
    {
        const string openingMarker = "<!-- canonical-profile:start -->\n```ini\n";
        const string closingMarker = "\n```\n<!-- canonical-profile:end -->";
        var editorConfigPath = Path.Combine(AppContext.BaseDirectory, "CompleteExample.editorconfig");
        var configurationGuidePath = Path.Combine(AppContext.BaseDirectory, "ConfigurationGuide.md");
        var editorConfig = NormalizeLineEndings(File.ReadAllText(editorConfigPath)).TrimEnd('\n');
        var configurationGuide = NormalizeLineEndings(File.ReadAllText(configurationGuidePath));
        var profileStart = configurationGuide.IndexOf(openingMarker, StringComparison.Ordinal);

        Assert.True(profileStart >= 0, "The canonical profile opening marker is missing.");
        profileStart += openingMarker.Length;

        var profileEnd = configurationGuide.IndexOf(closingMarker, profileStart, StringComparison.Ordinal);

        Assert.True(profileEnd >= profileStart, "The canonical profile closing marker is missing.");
        Assert.Equal(editorConfig, configurationGuide[profileStart..profileEnd]);
    }

    private static IReadOnlyDictionary<string, string> ReadStructuredTextSection(string path)
    {
        const string section = "[*.{st,iecst,TcPOU,TcDUT,TcGVL,TcITF,TcPRG}]";
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inSection = false;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();

            if (line.StartsWith('['))
            {
                inSection = string.Equals(line, section, StringComparison.Ordinal);
                continue;
            }

            if (!inSection || line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            Assert.True(separator > 0, $"Invalid EditorConfig line: {rawLine}");
            values.Add(line[..separator].Trim(), line[(separator + 1)..].Trim());
        }

        return values;
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
