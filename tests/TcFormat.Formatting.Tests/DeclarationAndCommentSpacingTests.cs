using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class DeclarationAndCommentSpacingTests
{
    [Theory]
    [InlineData(false, false, "x AT %I* : BOOL;", "longName AT %QX0.0 : BOOL;")]
    [InlineData(false, true, "x AT %I* : BOOL;", "longName AT %QX0.0 : BOOL;")]
    [InlineData(true, false, "x AT %I*           : BOOL;", "longName AT %QX0.0 : BOOL;")]
    [InlineData(true, true, "x        AT %I*    : BOOL;", "longName AT %QX0.0 : BOOL;")]
    public void AddressPaddingFollowsDeclarationAlignment(bool declarations, bool addresses, string first, string second)
    {
        var options = FormatterOptions.Default with
        {
            Alignment = FormatterOptions.Default.Alignment with { Declarations = declarations, Addresses = addresses }
        };
        AssertFormatted("VAR\nx      AT    %I*     :BOOL;\nlongName AT %QX0.0:BOOL;\nEND_VAR",
            $"VAR\r\n    {first}\r\n    {second}\r\nEND_VAR\r\n", options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeepsGapBeforeTrailingCommentsWithOrWithoutAlignment(bool alignComments)
    {
        var options = FormatterOptions.Default with
        {
            Alignment = FormatterOptions.Default.Alignment with { Assignments = false, EndOfLineComments = alignComments },
            Spacing = FormatterOptions.Default.Spacing with { SpacesBeforeEndOfLineComment = 2 }
        };
        var gap = alignComments ? "      " : "  ";
        AssertFormatted("x:=1;// first\nother:=2;// second",
            $"x := 1;{gap}// first\r\nother := 2;  // second\r\n", options);
    }

    [Fact]
    public void IndentsCommentOnlyLinesWithTheirControlBlock()
    {
        AssertFormatted("IF ready THEN\n// outer\nIF enabled THEN\n// inner\nRun();// trailing\nEND_IF\n// outer again\nEND_IF",
            "IF ready THEN\r\n    // outer\r\n    IF enabled THEN\r\n        // inner\r\n        Run(); // trailing\r\n" +
            "    END_IF\r\n    // outer again\r\nEND_IF\r\n", FormatterOptions.Default);
    }

    [Fact]
    public void IndentsCommentsInDeclarationsAndContinuedHeadersUsingTabs()
    {
        var options = FormatterOptions.Default with
        {
            Indentation = FormatterOptions.Default.Indentation with { Style = IndentStyle.Tabs }
        };
        AssertFormatted("VAR\n// variables\nx:BOOL;\nEND_VAR\nIF ready AND\n//   keep comment contents\nenabled THEN\n// body\nEND_IF",
            "VAR\r\n\t// variables\r\n\tx : BOOL;\r\nEND_VAR\r\nIF ready AND\r\n\t//   keep comment contents\r\n" +
            "\tenabled THEN\r\n\t// body\r\nEND_IF\r\n", options);
    }

    private static void AssertFormatted(string source, string expected, FormatterOptions options)
    {
        var result = StructuredTextFormatter.Format(source, options);
        Assert.True(result.IsValid, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(expected, result.FormattedText);
        var second = StructuredTextFormatter.Format(result.FormattedText, options);
        Assert.True(second.IsValid);
        Assert.False(second.Changed);
    }
}
