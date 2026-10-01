using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class AssignmentContinuationTests
{
    [Theory]
    [InlineData("AND")]
    [InlineData("OR")]
    [InlineData("XOR")]
    [InlineData("AND_THEN")]
    [InlineData("OR_ELSE")]
    [InlineData("+")]
    public void IndentsAssignmentContinuationsWithLeadingOrTrailingOperators(string op)
    {
        var source = $"value := first\n{op} second {op}\nthird;\nRun();";
        var expected = $"value := first\r\n    {op} second {op}\r\n    third;\r\nRun();\r\n";

        AssertFormatted(source, expected);
    }

    [Theory]
    [InlineData(IndentStyle.Spaces, 4, "    ", "        ")]
    [InlineData(IndentStyle.Spaces, 2, "    ", "      ")]
    [InlineData(IndentStyle.Tabs, 4, "\t", "\t\t")]
    public void IndentsNestedAssignmentsAndCommentsUntilTheSemicolon(
        IndentStyle style, int continuationSize, string indent, string continuation)
    {
        const string source = "FOR i := 0 TO count - 1 DO\n" +
                              "stationary := master.Stopped AND NOT master.Busy\n" +
                              "// check coupled axes\nAND axes[i].InGear\n" +
                              "(* position tolerance *)\nAND ABS(axes[i].ActualPosition - target) <= tolerance;\n" +
                              "Run();\nEND_FOR";
        var expected = "FOR i := 0 TO count - 1 DO\r\n" +
                       indent + "stationary := master.Stopped AND NOT master.Busy\r\n" +
                       continuation + "// check coupled axes\r\n" +
                       continuation + "AND axes[i].InGear\r\n" +
                       continuation + "(* position tolerance *)\r\n" +
                       continuation + "AND ABS(axes[i].ActualPosition - target) <= tolerance;\r\n" +
                       indent + "Run();\r\nEND_FOR\r\n";
        var options = Options with
        {
            Indentation = Options.Indentation with { Style = style, ContinuationSize = continuationSize }
        };

        AssertFormatted(source, expected, options);
    }

    [Theory]
    [InlineData(":=")]
    [InlineData("REF=")]
    [InlineData("?=")]
    public void IndentsRightHandSideOnItsOwnLine(string assignment)
    {
        AssertFormatted($"value {assignment}\nsource;\nRun();",
            $"value {assignment}\r\n    source;\r\nRun();\r\n");
    }

    [Fact]
    public void ContinuesAfterParenthesesCloseAndResetsBeforeTheNextStatement()
    {
        AssertFormatted("value := (first\nAND second)\nOR third;\nother := TRUE;",
            "value := (first\r\n    AND second)\r\n    OR third;\r\nother := TRUE;\r\n");
    }

    [Fact]
    public void DeclarationInitializersContinueButMethodReturnTypesDoNot()
    {
        AssertFormatted("METHOD Calculate : BOOL\nVAR\nvalue : BOOL := first\nOR second;\nother : BOOL;\nEND_VAR",
            "METHOD Calculate : BOOL\r\nVAR\r\n    value : BOOL := first\r\n        OR second;\r\n    other : BOOL;\r\nEND_VAR\r\n");
    }

    [Fact]
    public void NamedArgumentsDoNotIndentFollowingStatements()
    {
        AssertFormatted("Call(input := first,\noutput => second);\nRun();",
            "Call(input := first,\r\n    output => second);\r\nRun();\r\n");
    }

    [Fact]
    public void MultilineForHeaderDoesNotIndentItsBodyTwice()
    {
        AssertFormatted("FOR i :=\n0 TO count - 1 DO\nRun();\nEND_FOR",
            "FOR i :=\r\n    0 TO count - 1 DO\r\n    Run();\r\nEND_FOR\r\n");
    }

    private static FormatterOptions Options => FormatterOptions.Default with
    {
        Alignment = new AlignmentOptions(false, false, false, false, false, false, false),
        Wrapping = FormatterOptions.Default.Wrapping with
        {
            Calls = WrapStyle.Preserve,
            Initializers = WrapStyle.Preserve,
            BinaryExpressions = WrapStyle.Preserve
        }
    };

    private static void AssertFormatted(string source, string expected, FormatterOptions? options = null)
    {
        options ??= Options;
        var first = StructuredTextFormatter.Format(source, options);
        Assert.True(first.IsValid, string.Join("; ", first.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(expected, first.FormattedText);
        var second = StructuredTextFormatter.Format(first.FormattedText, options);
        Assert.True(second.IsValid);
        Assert.False(second.Changed);
    }
}
