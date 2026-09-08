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
        var afterHeader = gap;
        var expected = "Prepare();" + gap + opening + afterHeader +
                       "    value := 1;" + gap + closing + gap + "done := TRUE;\r\n";
        var source = $"Prepare();\n\n{opening}\n\nvalue := 1;\n\n{closing}\n\ndone := TRUE;";

        AssertProfileOutput(profile, source, expected);
    }

    [Theory]
    [InlineData("less-whitespace", false)]
    [InlineData("more-whitespace", true)]
    public void ProfilesSeparateNestedLoopsAfterThenAndKeepElseTight(string profile, bool more)
    {
        var gap = more ? "\r\n\r\n" : "\r\n";
        const string source = "IF axesAreSimulated THEN\nFOR i := 0 TO count - 1 DO\n" +
                              "IF NOT simulated THEN\naxesAreSimulated := FALSE;\nEXIT;\nEND_IF\nEND_FOR\n" +
                              "ELSE\n\nFOR i := 0 TO count - 1 DO\nRun();\nEND_FOR\nEND_IF";
        var expected = "IF axesAreSimulated THEN" + gap +
                       "    FOR i := 0 TO count - 1 DO" + gap +
                       "        IF NOT simulated THEN\r\n            axesAreSimulated := FALSE;\r\n" +
                       "            EXIT;" + gap + "        END_IF" + gap + "    END_FOR" + gap +
                       "ELSE\r\n    FOR i := 0 TO count - 1 DO" + gap +
                       "        Run();" + gap + "    END_FOR" + gap + "END_IF\r\n";

        AssertProfileOutput(profile, source, expected);
    }

    [Theory]
    [InlineData("less-whitespace")]
    [InlineData("more-whitespace")]
    [InlineData("more-whitespace-no-assignment-alignment")]
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
        Assert.Equal(profile.StartsWith("more", StringComparison.Ordinal) ? BlankLinePolicy.Require : BlankLinePolicy.Remove,
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

    [Theory]
    [InlineData("less-whitespace", false)]
    [InlineData("more-whitespace", true)]
    public void ProfilesConfigureExpandedCalculations(string profile, bool more)
    {
        const string source = "pressureSpeedOverride := LREAL_TO_INT(100.0 * _clampingPressureVelocity /\nTO_LREAL(_speedCommandOption));";
        var expected = more
            ? "pressureSpeedOverride := LREAL_TO_INT(\r\n    100.0 * _clampingPressureVelocity\r\n    / TO_LREAL(_speedCommandOption)\r\n);\r\n"
            : "pressureSpeedOverride := LREAL_TO_INT(100.0 * _clampingPressureVelocity /\r\n    TO_LREAL(_speedCommandOption));\r\n";

        AssertProfileOutput(profile, source, expected);
    }

    [Theory]
    [InlineData("more-whitespace-no-assignment-alignment", "", "", "")]
    [InlineData("more-whitespace", "  ", "       ", "       ")]
    public void MoreWhitespaceProfilesChooseAssignmentAlignment(string profile, string initializerPadding, string assignmentPadding, string inputPadding)
    {
        const string source = "VAR\nshort : INT := 1;\nlongName : LREAL := 2;\nEND_VAR\n" +
                              "IF ready THEN\nx := 1;\nlongName := 2;\nMove(Position := target,\nv := speed);\n" +
                              "Read(Position => target,\nv => speed);\nEND_IF";
        var expected = "VAR\r\n    short    : INT " + initializerPadding + ":= 1;\r\n    longName : LREAL := 2;\r\nEND_VAR\r\n\r\n" +
                       "IF ready THEN\r\n    x " + assignmentPadding + ":= 1;\r\n    longName := 2;\r\n" +
                       "    Move(Position := target,\r\n         v " + inputPadding + ":= speed);\r\n\r\n" +
                       "    Read(Position => target,\r\n         v " + inputPadding + "=> speed);\r\n\r\nEND_IF\r\n";

        AssertProfileOutput(profile, source, expected);
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
