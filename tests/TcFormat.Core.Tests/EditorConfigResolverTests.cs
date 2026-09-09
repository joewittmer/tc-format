using TcFormat.Core;

using Xunit;

namespace TcFormat.Core.Tests;

public sealed class EditorConfigResolverTests
{
    [Fact]
    public void ResolvesMultilinePolicyForControlHeaders()
    {
        using var directory = new TemporaryDirectory();
        directory.Write(".editorconfig", """
            root = true

            [*.st]
            tc_format_blank_line_after_if_then = multiline
            tc_format_blank_line_after_elsif_then = MULTILINE
            tc_format_blank_line_after_do = multiline
            """);

        var result = new EditorConfigResolver().Resolve(directory.Write("Example.st", string.Empty));

        Assert.True(result.IsValid);
        Assert.Equal(BlankLinePolicy.Multiline, result.Options.BlankLines.AfterIfThen);
        Assert.Equal(BlankLinePolicy.Multiline, result.Options.BlankLines.AfterElsifThen);
        Assert.Equal(BlankLinePolicy.Multiline, result.Options.BlankLines.AfterDo);
    }

    [Theory]
    [InlineData("tc_format_blank_line_before_if")]
    [InlineData("tc_format_blank_line_after_multiline_call")]
    public void RejectsMultilinePolicyOutsideControlHeaders(string key)
    {
        using var directory = new TemporaryDirectory();
        directory.Write(".editorconfig", $"root = true\n[*.st]\n{key} = multiline");

        var result = new EditorConfigResolver().Resolve(directory.Write("Example.st", string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.PropertyName == key);
    }

    [Theory]
    [InlineData("true", BlankLinePolicy.Require)]
    [InlineData("false", BlankLinePolicy.Remove)]
    [InlineData("preserve", BlankLinePolicy.Preserve)]
    public void ResolvesLoopAndClosingBlockBlankLinePolicies(string value, BlankLinePolicy expected)
    {
        using var directory = new TemporaryDirectory();
        directory.Write(".editorconfig", $"""
            root = true

            [*.st]
            tc_format_blank_line_before_loop = {value}
            tc_format_blank_line_after_repeat = {value}
            tc_format_blank_line_before_until = {value}
            tc_format_blank_line_before_end_loop = {value}
            tc_format_blank_line_after_control_flow_block = {value}
            tc_format_blank_line_after_multiline_call = {value}
            """);

        var result = new EditorConfigResolver().Resolve(directory.Write("Example.st", string.Empty));

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Options.BlankLines.BeforeLoop);
        Assert.Equal(expected, result.Options.BlankLines.AfterRepeat);
        Assert.Equal(expected, result.Options.BlankLines.BeforeUntil);
        Assert.Equal(expected, result.Options.BlankLines.BeforeEndLoop);
        Assert.Equal(expected, result.Options.BlankLines.AfterControlFlowBlock);
        Assert.Equal(expected, result.Options.BlankLines.AfterMultilineCall);
    }

    [Fact]
    public void NestedConfigurationOverridesAndInheritsRepositoryValues()
    {
        using var directory = new TemporaryDirectory();
        directory.Write(
            ".editorconfig",
            """
            root = true

            [*.TcPOU]
            max_line_length = 120
            tc_format_keyword_case = lower
            tc_format_align_assignments = true
            """);
        directory.Write(
            "MachineA/.editorconfig",
            """
            [*.TcPOU]
            max_line_length = 150
            tc_format_align_assignments = false
            """);
        var sourcePath = directory.Write("MachineA/Program.TcPOU", string.Empty);

        var result = new EditorConfigResolver().Resolve(sourcePath);

        Assert.True(result.IsValid);
        Assert.Equal(150, result.Options.Layout.MaximumLineLength);
        Assert.Equal(KeywordCase.Lower, result.Options.KeywordCase);
        Assert.False(result.Options.Alignment.Assignments);
        Assert.EndsWith(
            Path.Combine("MachineA", ".editorconfig"),
            Find(result, "tc_format_align_assignments").Source,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RootTrueStopsConfigurationSearch()
    {
        using var directory = new TemporaryDirectory();
        directory.Write(
            ".editorconfig",
            """
            [*.TcPOU]
            max_line_length = 80
            """);
        directory.Write(
            "Repository/.editorconfig",
            """
            root = true

            [*.TcPOU]
            tc_format_keyword_case = preserve
            """);
        var sourcePath = directory.Write("Repository/Program.TcPOU", string.Empty);

        var result = new EditorConfigResolver().Resolve(sourcePath);

        Assert.True(result.IsValid);
        Assert.Equal(110, result.Options.Layout.MaximumLineLength);
        Assert.Equal(KeywordCase.Preserve, result.Options.KeywordCase);
    }

    [Fact]
    public void UnsetRestoresBuiltInValue()
    {
        using var directory = new TemporaryDirectory();
        directory.Write(
            ".editorconfig",
            """
            root = true

            [*.TcPOU]
            tc_format_align_assignments = false
            """);
        var nestedConfigPath = directory.Write(
            "MachineA/.editorconfig",
            """
            [*.TcPOU]
            tc_format_align_assignments = unset
            """);
        var sourcePath = directory.Write("MachineA/Program.TcPOU", string.Empty);

        var result = new EditorConfigResolver().Resolve(sourcePath);
        var value = Find(result, "tc_format_align_assignments");

        Assert.True(result.IsValid);
        Assert.True(result.Options.Alignment.Assignments);
        Assert.Equal("<built-in>", value.Source);
        Assert.Equal(nestedConfigPath, value.UnsetBy);
    }

    [Fact]
    public void UnknownTcFormatPropertyIsIgnoredForForwardCompatibility()
    {
        using var directory = new TemporaryDirectory();
        directory.Write(
            ".editorconfig",
            """
            root = true

            [*.TcPOU]
            tc_format_align_assigments = true
            tc_format_align_assignments = false
            """);
        var sourcePath = directory.Write("Program.TcPOU", string.Empty);

        var result = new EditorConfigResolver().Resolve(sourcePath);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
        Assert.False(result.Options.Alignment.Assignments);
    }

    [Fact]
    public void StructuralBlankLinesCanBeConfiguredPerKeyword()
    {
        using var directory = new TemporaryDirectory();
        directory.Write(
            ".editorconfig",
            """
            root = true

            [*.TcPOU]
            tc_format_blank_line_before_var = false
            tc_format_blank_line_before_if = preserve
            tc_format_blank_line_before_case = false
            tc_format_blank_line_before_if_else = true
            tc_format_blank_line_before_case_else = false
            tc_format_blank_line_before_elsif = false
            tc_format_blank_line_before_case_label = preserve
            tc_format_blank_line_before_end_var = true
            tc_format_blank_line_before_end_if = true
            tc_format_blank_line_before_end_case = true
            tc_format_blank_line_after_if_then = preserve
            tc_format_blank_line_after_elsif_then = true
            tc_format_blank_line_after_do = preserve
            tc_format_blank_line_after_case_label = true
            """);
        var sourcePath = directory.Write("Program.TcPOU", string.Empty);

        var result = new EditorConfigResolver().Resolve(sourcePath);

        Assert.True(result.IsValid);
        Assert.Equal(BlankLinePolicy.Remove, result.Options.BlankLines.BeforeVariableBlock);
        Assert.Equal(BlankLinePolicy.Preserve, result.Options.BlankLines.BeforeIf);
        Assert.Equal(BlankLinePolicy.Remove, result.Options.BlankLines.BeforeCase);
        Assert.Equal(BlankLinePolicy.Require, result.Options.BlankLines.BeforeIfElse);
        Assert.Equal(BlankLinePolicy.Remove, result.Options.BlankLines.BeforeCaseElse);
        Assert.Equal(BlankLinePolicy.Remove, result.Options.BlankLines.BeforeElsif);
        Assert.Equal(BlankLinePolicy.Preserve, result.Options.BlankLines.BeforeCaseLabel);
        Assert.Equal(BlankLinePolicy.Require, result.Options.BlankLines.BeforeEndVar);
        Assert.Equal(BlankLinePolicy.Require, result.Options.BlankLines.BeforeEndIf);
        Assert.Equal(BlankLinePolicy.Require, result.Options.BlankLines.BeforeEndCase);
        Assert.Equal(BlankLinePolicy.Preserve, result.Options.BlankLines.AfterIfThen);
        Assert.Equal(BlankLinePolicy.Require, result.Options.BlankLines.AfterElsifThen);
        Assert.Equal(BlankLinePolicy.Preserve, result.Options.BlankLines.AfterDo);
        Assert.Equal(BlankLinePolicy.Require, result.Options.BlankLines.AfterCaseLabel);
    }

    [Fact]
    public void RemovedEnabledPropertyIsIgnored()
    {
        using var directory = new TemporaryDirectory();
        directory.Write(
            ".editorconfig",
            """
            root = true

            [*.TcPOU]
            tc_format_enabled = false
            """);
        var sourcePath = directory.Write("Program.TcPOU", string.Empty);

        var result = new EditorConfigResolver().Resolve(sourcePath);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void InvalidValueIsAnErrorAndFallsBackSafely()
    {
        using var directory = new TemporaryDirectory();
        directory.Write(
            ".editorconfig",
            """
            root = true

            [*.TcPOU]
            tc_format_wrap_calls = sometimes
            """);
        var sourcePath = directory.Write("Program.TcPOU", string.Empty);

        var result = new EditorConfigResolver().Resolve(sourcePath);

        Assert.False(result.IsValid);
        Assert.Equal(WrapStyle.Hanging, result.Options.Wrapping.Calls);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.PropertyName == "tc_format_wrap_calls");
    }

    [Fact]
    public void HangingCallWrappingCanBeConfigured()
    {
        using var directory = new TemporaryDirectory();
        directory.Write(
            ".editorconfig",
            """
            root = true

            [*.TcPOU]
            tc_format_wrap_calls = hanging
            """);
        var sourcePath = directory.Write("Program.TcPOU", string.Empty);

        var result = new EditorConfigResolver().Resolve(sourcePath);

        Assert.True(result.IsValid);
        Assert.Equal(WrapStyle.Hanging, result.Options.Wrapping.Calls);
    }

    [Fact]
    public void HangingInitializerWrappingCanBeConfiguredIndependently()
    {
        using var directory = new TemporaryDirectory();
        directory.Write(
            ".editorconfig",
            """
            root = true

            [*.TcPOU]
            tc_format_wrap_calls = preserve
            tc_format_wrap_initializers = hanging
            tc_format_wrap_binary_expressions = always
            """);
        var sourcePath = directory.Write("Program.TcPOU", string.Empty);

        var result = new EditorConfigResolver().Resolve(sourcePath);

        Assert.True(result.IsValid);
        Assert.Equal(WrapStyle.Preserve, result.Options.Wrapping.Calls);
        Assert.Equal(WrapStyle.Hanging, result.Options.Wrapping.Initializers);
        Assert.Equal(WrapStyle.Always, result.Options.Wrapping.BinaryExpressions);
    }

    [Theory]
    [InlineData("true", true, true)]
    [InlineData("false", false, true)]
    [InlineData("sometimes", false, false)]
    public void ResolvesExpandedMultilineArguments(string value, bool expected, bool valid)
    {
        using var directory = new TemporaryDirectory();
        directory.Write(".editorconfig", "root = true\n[*.st]\ntc_format_expand_multiline_arguments = " + value);
        var sourcePath = directory.Write("Example.st", string.Empty);
        var result = new EditorConfigResolver().Resolve(sourcePath);

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(expected, result.Options.Wrapping.ExpandMultilineArguments);
        if (!valid)
        {
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.PropertyName == "tc_format_expand_multiline_arguments");
        }
    }

    [Theory]
    [InlineData("preserve", ClosingDelimiterStyle.Preserve, true)]
    [InlineData("own_line", ClosingDelimiterStyle.OwnLine, true)]
    [InlineData("same_line", ClosingDelimiterStyle.SameLine, true)]
    [InlineData("sometimes", ClosingDelimiterStyle.Preserve, false)]
    public void ResolvesClosingDelimiterPlacement(string value, ClosingDelimiterStyle expected, bool valid)
    {
        using var directory = new TemporaryDirectory();
        directory.Write(".editorconfig", "root = true\n[*.st]\ntc_format_multiline_closing_parenthesis = " + value +
                                        "\ntc_format_multiline_closing_bracket = " + value);
        var result = new EditorConfigResolver().Resolve(directory.Write("Example.st", string.Empty));

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(expected, result.Options.Wrapping.MultilineClosingParenthesis);
        Assert.Equal(expected, result.Options.Wrapping.MultilineClosingBracket);
        if (!valid)
        {
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.PropertyName == "tc_format_multiline_closing_parenthesis");
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.PropertyName == "tc_format_multiline_closing_bracket");
        }
    }

    [Fact]
    public void MaximumLineLengthCanBeDisabled()
    {
        using var directory = new TemporaryDirectory();
        directory.Write(
            ".editorconfig",
            """
            root = true

            [*.TcPOU]
            max_line_length = off
            """);
        var sourcePath = directory.Write("Program.TcPOU", string.Empty);

        var result = new EditorConfigResolver().Resolve(sourcePath);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.Options.Layout.MaximumLineLength);
    }

    private static ResolvedOptionValue Find(ResolvedFormatterConfiguration result, string name) =>
        Assert.Single(result.Values, option => option.Name == name);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tc-format-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Write(string relativePath, string content)
        {
            var fullPath = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

