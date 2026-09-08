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
    public void MultilinePolicyIsValidOnlyForControlHeaders()
    {
        var valid = FormatterOptions.Default with
        {
            BlankLines = FormatterOptions.Default.BlankLines with
            {
                AfterIfThen = BlankLinePolicy.Multiline,
                AfterElsifThen = BlankLinePolicy.Multiline,
                AfterDo = BlankLinePolicy.Multiline
            }
        };
        Assert.Empty(valid.Validate());

        var invalid = valid with
        {
            BlankLines = valid.BlankLines with { BeforeIf = BlankLinePolicy.Multiline }
        };
        Assert.Single(invalid.Validate());
    }

    [Fact]
    public void NewBlankLinePoliciesPreserveExistingDefaultsAndRejectInvalidValues()
    {
        var defaults = FormatterOptions.Default.BlankLines;
        Assert.Equal(BlankLinePolicy.Preserve, defaults.BeforeLoop);
        Assert.Equal(BlankLinePolicy.Preserve, defaults.AfterRepeat);
        Assert.Equal(BlankLinePolicy.Preserve, defaults.BeforeUntil);
        Assert.Equal(BlankLinePolicy.Preserve, defaults.BeforeEndLoop);
        Assert.Equal(BlankLinePolicy.Preserve, defaults.AfterControlFlowBlock);
        Assert.Equal(BlankLinePolicy.Preserve, defaults.AfterMultilineCall);
        var invalid = FormatterOptions.Default with
        {
            BlankLines = defaults with
            {
                BeforeLoop = (BlankLinePolicy)999,
                AfterRepeat = (BlankLinePolicy)999,
                BeforeUntil = (BlankLinePolicy)999,
                BeforeEndLoop = (BlankLinePolicy)999,
                AfterControlFlowBlock = (BlankLinePolicy)999,
                AfterMultilineCall = (BlankLinePolicy)999
            }
        };

        Assert.Equal(6, invalid.Validate().Count);
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

    [Theory]
    [InlineData("less-whitespace.editorconfig")]
    [InlineData("more-whitespace.editorconfig")]
    [InlineData("more-whitespace-no-assignment-alignment.editorconfig")]
    public void WhitespaceProfileExplicitlySetsEverySupportedOption(string profile)
    {
        var editorConfigPath = Path.Combine(AppContext.BaseDirectory, profile);
        var configuredValues = ReadStructuredTextSection(editorConfigPath);

        Assert.Equal(EditorConfigOptionCatalog.BuiltInValues.Count, configuredValues.Count);
        Assert.Empty(EditorConfigOptionCatalog.BuiltInValues.Keys.Except(configuredValues.Keys, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(configuredValues.Keys.Except(EditorConfigOptionCatalog.BuiltInValues.Keys, StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("less-whitespace.editorconfig")]
    [InlineData("more-whitespace.editorconfig")]
    [InlineData("more-whitespace-no-assignment-alignment.editorconfig")]
    public void WhitespaceProfileKeepsSharedFormattingChoices(string profile)
    {
        var editorConfigPath = Path.Combine(AppContext.BaseDirectory, profile);
        var configuredValues = ReadStructuredTextSection(editorConfigPath);

        Assert.Equal("space", configuredValues["indent_style"]);
        Assert.Equal("off", configuredValues["max_line_length"]);
        Assert.Equal("true", configuredValues["tc_format_align_end_of_line_comments"]);
        Assert.Equal("hanging", configuredValues["tc_format_wrap_calls"]);
        Assert.Equal("always", configuredValues["tc_format_wrap_initializers"]);
        Assert.Equal("preserve", configuredValues["tc_format_wrap_binary_expressions"]);
        Assert.Equal(profile.StartsWith("more", StringComparison.Ordinal) ? "before" : "after",
            configuredValues["tc_format_binary_operator_position"]);
        Assert.Equal(profile.StartsWith("more", StringComparison.Ordinal) ? "true" : "false",
            configuredValues["tc_format_expand_multiline_arguments"]);
    }

    [Theory]
    [InlineData("less-whitespace.editorconfig")]
    [InlineData("more-whitespace.editorconfig")]
    [InlineData("more-whitespace-no-assignment-alignment.editorconfig")]
    public void WhitespaceProfileAnnotatesEveryOptionWithAcceptedValuesAndBuiltInDefault(string profile)
    {
        var annotations = new List<string>();
        var checkedOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadLines(Path.Combine(AppContext.BaseDirectory, profile)))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#'))
            {
                annotations.Add(line);
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                var key = line[..separator].Trim();
                if (EditorConfigOptionCatalog.BuiltInValues.TryGetValue(key, out var defaultValue))
                {
                    Assert.Contains(annotations, annotation => annotation.StartsWith("# Options:", StringComparison.Ordinal));
                    Assert.Contains("# Default: " + defaultValue, annotations);
                    Assert.True(checkedOptions.Add(key), $"Duplicate option: {key}");
                }
            }

            annotations.Clear();
        }

        Assert.Equal(EditorConfigOptionCatalog.BuiltInValues.Count, checkedOptions.Count);
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
}
