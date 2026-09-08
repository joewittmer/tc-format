using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class WhitespaceProfileTests
{
    [Theory]
    [InlineData("less-whitespace", false)]
    [InlineData("more-whitespace", true)]
    public void ProfilesFormatIfBranchesAndKeepNestedIfDirectlyAfterElse(string profile, bool more)
    {
        var gap = more ? "\r\n\r\n" : "\r\n";
        var expected = "ready := TRUE;" + gap + "IF first THEN\r\n" +
                       "    value := 1;" + gap + "ELSIF second THEN\r\n" +
                       "    value := 2;" + gap + "ELSE\r\n" +
                       "    IF third THEN\r\n        value := 3;" + gap +
                       "    END_IF" + gap + "END_IF" + gap + "done := TRUE;\r\n";
        const string source = "ready := TRUE;\nIF first THEN\n\nvalue := 1;\nELSIF second THEN\n\n" +
                              "value := 2;\nELSE\n\nIF third THEN\nvalue := 3;\nEND_IF\nEND_IF\ndone := TRUE;";

        AssertProfileOutput(profile, source, expected);
    }

    [Theory]
    [InlineData("less-whitespace", false)]
    [InlineData("more-whitespace", true)]
    public void ProfilesFormatCaseLabelsAndKeepCaseElseTight(string profile, bool more)
    {
        var gap = more ? "\r\n\r\n" : "\r\n";
        var expected = "ready := TRUE;" + gap + "CASE mode OF" + gap +
                       "    1:" + gap + "        value := 1;" + gap +
                       "    2:" + gap + "        value := 2;" + gap +
                       "    ELSE\r\n        value := 3;" + gap + "END_CASE" + gap + "done := TRUE;\r\n";
        const string source = "ready := TRUE;\nCASE mode OF\n1:\nvalue := 1;\n2:\nvalue := 2;\n" +
                              "ELSE\n\nvalue := 3;\nEND_CASE\ndone := TRUE;";

        AssertProfileOutput(profile, source, expected);
    }

    [Theory]
    [InlineData("less-whitespace", false, "FOR index := 1 TO 3 DO", "END_FOR")]
    [InlineData("more-whitespace", true, "FOR index := 1 TO 3 DO", "END_FOR")]
    [InlineData("less-whitespace", false, "WHILE ready DO", "END_WHILE")]
    [InlineData("more-whitespace", true, "WHILE ready DO", "END_WHILE")]
    [InlineData("less-whitespace", false, "REPEAT", "UNTIL ready\r\nEND_REPEAT")]
    [InlineData("more-whitespace", true, "REPEAT", "UNTIL ready\r\n\r\nEND_REPEAT")]
    public void ProfilesFormatLoopBoundaries(string profile, bool more, string opening, string closing)
    {
        var gap = more ? "\r\n\r\n" : "\r\n";
        var afterHeader = opening == "REPEAT" ? gap : "\r\n";
        var expected = "Prepare();" + gap + opening + afterHeader +
                       "    value := 1;" + gap + closing + gap + "done := TRUE;\r\n";
        var source = $"Prepare();\n\n{opening}\n\nvalue := 1;\n\n{closing}\n\ndone := TRUE;";

        AssertProfileOutput(profile, source, expected);
    }

    [Theory]
    [InlineData("less-whitespace")]
    [InlineData("more-whitespace")]
    public void ProfilesExplicitlyConfigureEveryOptionAndKeepSelectedWrapping(string profile)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Profiles", profile);
        var keys = File.ReadLines(Path.Combine(directory, ".editorconfig"))
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith('#') && line.Contains('=') && !line.StartsWith("root", StringComparison.Ordinal))
            .Select(line => line.Split('=')[0].Trim())
            .ToArray();
        Assert.Equal(EditorConfigOptionCatalog.BuiltInValues.Count, keys.Length);
        Assert.Empty(EditorConfigOptionCatalog.BuiltInValues.Keys.Except(keys));

        var resolved = new EditorConfigResolver().Resolve(Path.Combine(directory, "Example.st"));
        Assert.True(resolved.IsValid);
        Assert.Empty(resolved.Options.Validate());
        Assert.Equal(WrapStyle.Always, resolved.Options.Wrapping.Initializers);
        Assert.Equal(WrapStyle.Hanging, resolved.Options.Wrapping.Calls);
        Assert.Equal(profile == "more-whitespace" ? BlankLinePolicy.Require : BlankLinePolicy.Remove,
            resolved.Options.BlankLines.AfterMultilineCall);
    }

    [Theory]
    [InlineData("less-whitespace", "\r\n")]
    [InlineData("more-whitespace", "\r\n\r\n")]
    public void ProfilesSeparateMultilineCallsAndKeepShortCallsTogether(string profile, string gap)
    {
        const string source = "_axis.Enable();\n_axis.Reset();\n_axis.Move(100,\n20);\ncompleted := FALSE;";
        var expected = "_axis.Enable();\r\n_axis.Reset();\r\n_axis.Move(100,\r\n           20);" +
                       gap + "completed := FALSE;\r\n";

        AssertProfileOutput(profile, source, expected);
    }

    [Theory]
    [InlineData("less-whitespace", false)]
    [InlineData("more-whitespace", true)]
    public void ProfilesSeparateMultilineConditionFromBody(string profile, bool more)
    {
        var gap = more ? "\r\n\r\n" : "\r\n";
        const string source = "IF ready AND\nenabled THEN\nRun();\nEND_IF";
        var expected = "IF ready AND\r\n    enabled THEN" + gap + "    Run();" + gap + "END_IF\r\n";

        AssertProfileOutput(profile, source, expected);
    }

    [Fact]
    public void RepeatConditionDoesNotCloseTheBlockBeforeEndRepeat()
    {
        const string source = "REPEAT\nREPEAT\nvalue := 1;\nUNTIL innerDone\nEND_REPEAT\nUNTIL done\nEND_REPEAT";
        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Contains("    UNTIL innerDone\r\n    END_REPEAT\r\nUNTIL done\r\nEND_REPEAT", result.FormattedText);
        Assert.False(StructuredTextFormatter.Format("REPEAT\nvalue := 1;\nUNTIL done", FormatterOptions.Default).IsValid);
        Assert.False(StructuredTextFormatter.Format("UNTIL done", FormatterOptions.Default).IsValid);
        Assert.False(StructuredTextFormatter.Format("IF ready THEN\nUNTIL done\nEND_IF", FormatterOptions.Default).IsValid);
    }

    private static void AssertProfileOutput(string profile, string source, string expected)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Profiles", profile, "Example.st");
        var resolved = new EditorConfigResolver().Resolve(path);
        Assert.True(resolved.IsValid);

        var first = StructuredTextFormatter.Format(source, resolved.Options);
        Assert.True(first.IsValid, string.Join("; ", first.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(expected, first.FormattedText);
        var second = StructuredTextFormatter.Format(first.FormattedText, resolved.Options);
        Assert.True(second.IsValid);
        Assert.Equal(first.FormattedText, second.FormattedText);
    }
}
