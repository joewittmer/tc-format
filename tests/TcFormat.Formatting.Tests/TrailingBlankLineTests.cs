using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class TrailingBlankLineTests
{
    [Theory]
    [InlineData("VAR\nx:BOOL;\n\nEND_VAR\n\n\n", "VAR\r\n    x : BOOL;\r\nEND_VAR\r\n")]
    [InlineData("Run();\n\n \t\n", "Run();\r\n")]
    [InlineData("Run();\n// final comment\n\n", "Run();\r\n// final comment\r\n")]
    [InlineData("(* comment\n\n*)\n\n", "(* comment\n\n*)\r\n")]
    public void RemovesTrailingBlankLinesButKeepsOneFileNewline(string source, string expected)
    {
        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);
        Assert.True(result.IsValid);
        Assert.Equal(expected, result.FormattedText);
        Assert.False(StructuredTextFormatter.Format(result.FormattedText, FormatterOptions.Default).Changed);
    }

    [Fact]
    public void PreservesLastContentLineSpacesWhenRequested()
    {
        var options = FormatterOptions.Default with
        {
            File = FormatterOptions.Default.File with { TrimTrailingWhitespace = false }
        };
        var result = StructuredTextFormatter.Format("Run();  \n  \n", options);
        Assert.True(result.IsValid);
        Assert.Equal("Run();  \r\n", result.FormattedText);
    }

    [Theory]
    [InlineData("// comment")]
    [InlineData("(* comment *)")]
    public void CommentSpacingAloneIsAFormattingChange(string comment)
    {
        var options = FormatterOptions.Default with
        {
            BlankLines = FormatterOptions.Default.BlankLines with { BeforeComment = BlankLinePolicy.Require }
        };
        var result = StructuredTextFormatter.Format($"Run();\r\n{comment}\r\n", options);
        Assert.True(result.IsValid);
        Assert.True(result.Changed);
        Assert.Equal($"Run();\r\n\r\n{comment}\r\n", result.FormattedText);
    }
}
