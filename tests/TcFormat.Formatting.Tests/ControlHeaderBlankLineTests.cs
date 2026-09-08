using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class ControlHeaderBlankLineTests
{
    [Theory]
    [InlineData("IF ready THEN", "IF ready THEN", "END_IF", false)]
    [InlineData("IF ready AND\nenabled THEN", "IF ready AND\r\n    enabled THEN", "END_IF", true)]
    [InlineData("IF ready\nTHEN", "IF ready\r\n    THEN", "END_IF", true)]
    [InlineData("WHILE ready DO", "WHILE ready DO", "END_WHILE", false)]
    [InlineData("WHILE ready AND\nenabled DO", "WHILE ready AND\r\n    enabled DO", "END_WHILE", true)]
    [InlineData("FOR index := 1 TO 3 DO", "FOR index := 1 TO 3 DO", "END_FOR", false)]
    [InlineData("FOR index := 1\nTO count DO", "FOR index := 1\r\n    TO count DO", "END_FOR", true)]
    [InlineData("IF ready // THEN\nAND enabled THEN // DO", "IF ready // THEN\r\n    AND enabled THEN // DO", "END_IF", true)]
    public void SeparatesOnlyMultilineHeaders(string header, string expectedHeader, string ending, bool multiline)
    {
        var gap = multiline ? "\r\n\r\n" : "\r\n";
        AssertFormatted(header + "\n\nRun();\n" + ending,
            expectedHeader + gap + "    Run();\r\n" + ending + "\r\n", Options());
    }

    [Fact]
    public void DistinguishesSingleLineIfAndMultilineElsif()
    {
        AssertFormatted("IF ready THEN\n\nRun();\nELSIF first AND\nsecond THEN\nWait();\nELSE\n\nStop();\nEND_IF",
            "IF ready THEN\r\n    Run();\r\nELSIF first AND\r\n    second THEN\r\n\r\n    Wait();\r\nELSE\r\n    Stop();\r\nEND_IF\r\n",
            Options());
    }

    [Theory]
    [InlineData(BlankLinePolicy.Require, "\n", "\r\n\r\n")]
    [InlineData(BlankLinePolicy.Remove, "\n\n", "\r\n")]
    [InlineData(BlankLinePolicy.Preserve, "\n", "\r\n")]
    [InlineData(BlankLinePolicy.Preserve, "\n\n", "\r\n\r\n")]
    public void ExistingPoliciesApplyAtThenEvenWhenItIsOnALaterLine(
        BlankLinePolicy policy, string sourceGap, string expectedGap)
    {
        var options = Options() with { BlankLines = Options().BlankLines with { AfterIfThen = policy } };
        AssertFormatted("IF first AND\nsecond THEN" + sourceGap + "Run();\nEND_IF",
            "IF first AND\r\n    second THEN" + expectedGap + "    Run();\r\nEND_IF\r\n", options);
    }

    [Theory]
    [InlineData("IF first AND second THEN", "IF first\r\n    AND second THEN")]
    [InlineData("IF (first AND second) THEN", "IF(first\r\n    AND second) THEN")]
    public void UsesFinalHeaderLayoutAfterBinaryWrapping(string sourceHeader, string expectedHeader)
    {
        var options = Options() with
        {
            Wrapping = Options().Wrapping with { BinaryExpressions = WrapStyle.Always }
        };
        AssertFormatted(sourceHeader + "\nRun();\nEND_IF",
            expectedHeader + "\r\n\r\n    Run();\r\nEND_IF\r\n", options);
    }

    [Fact]
    public void UsesFinalHeaderLayoutAfterCallWrapping()
    {
        var options = Options() with { Wrapping = Options().Wrapping with { Calls = WrapStyle.Always } };
        AssertFormatted("IF Check(1, 2) THEN\nRun();\nEND_IF",
            "IF Check(\r\n    1,\r\n    2\r\n) THEN\r\n\r\n    Run();\r\nEND_IF\r\n", options);
    }

    [Fact]
    public void RecognizesHeaderWrappedByMaximumLineLength()
    {
        var options = Options() with
        {
            Layout = Options().Layout with { MaximumLineLength = 32 },
            Wrapping = Options().Wrapping with { BinaryExpressions = WrapStyle.WhenLong }
        };
        AssertFormatted("IF firstCondition AND secondCondition THEN\nRun();\nEND_IF",
            "IF firstCondition\r\n    AND secondCondition THEN\r\n\r\n    Run();\r\nEND_IF\r\n", options);
    }

    [Fact]
    public void MultilineHeadersUseConfiguredTabContinuation()
    {
        var options = Options() with
        {
            Indentation = Options().Indentation with { Style = IndentStyle.Tabs }
        };
        AssertFormatted("IF Check(1,\n2) THEN\nRun();\nEND_IF",
            "IF Check(1,\r\n\t2) THEN\r\n\r\n\tRun();\r\nEND_IF\r\n", options);
    }

    [Fact]
    public void NestedParenthesizedHeaderIndentationRemainsStable()
    {
        var options = Options() with
        {
            Wrapping = Options().Wrapping with { BinaryExpressions = WrapStyle.Always }
        };
        AssertFormatted("IF ready THEN\nIF (first AND second) THEN\nRun();\nEND_IF\nEND_IF",
            "IF ready THEN\r\n    IF(first\r\n        AND second) THEN\r\n\r\n        Run();\r\n    END_IF\r\nEND_IF\r\n",
            options);
    }

    [Fact]
    public void KeywordTextInsideStringsAndCommentsDoesNotFinishTheHeader()
    {
        AssertFormatted("IF Check('THEN DO', (* THEN *)\nTRUE) THEN\nRun();\nEND_IF",
            "IF Check('THEN DO', (* THEN *)\r\n    TRUE) THEN\r\n\r\n    Run();\r\nEND_IF\r\n", Options());
    }

    [Fact]
    public void GlobalLimitAndConflictingBeforePolicyStillTakePrecedence()
    {
        var options = Options() with { Layout = Options().Layout with { MaximumConsecutiveBlankLines = 0 } };
        AssertFormatted("IF first AND\nsecond THEN\nRun();\nEND_IF",
            "IF first AND\r\n    second THEN\r\n    Run();\r\nEND_IF\r\n", options);
        AssertFormatted("IF first AND\nsecond THEN\nIF ready THEN\nRun();\nEND_IF\nEND_IF",
            "IF first AND\r\n    second THEN\r\n    IF ready THEN\r\n        Run();\r\n    END_IF\r\nEND_IF\r\n", Options());
    }

    private static FormatterOptions Options() => FormatterOptions.Default with
    {
        Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 0 },
        Wrapping = FormatterOptions.Default.Wrapping with
        {
            Calls = WrapStyle.Preserve,
            Initializers = WrapStyle.Preserve,
            BinaryExpressions = WrapStyle.Preserve
        },
        BlankLines = FormatterOptions.Default.BlankLines with
        {
            AfterIfThen = BlankLinePolicy.Multiline,
            AfterElsifThen = BlankLinePolicy.Multiline,
            AfterDo = BlankLinePolicy.Multiline
        }
    };

    private static void AssertFormatted(string source, string expected, FormatterOptions options)
    {
        var first = StructuredTextFormatter.Format(source, options);
        Assert.True(first.IsValid);
        Assert.Equal(expected, first.FormattedText);
        var second = StructuredTextFormatter.Format(first.FormattedText, options);
        Assert.True(second.IsValid);
        Assert.Equal(first.FormattedText, second.FormattedText);
    }
}
