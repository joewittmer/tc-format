using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class MultilineCallBlankLineTests
{
    [Theory]
    [InlineData(BlankLinePolicy.Require, "\n", "\r\n\r\n")]
    [InlineData(BlankLinePolicy.Require, "\n\n\n", "\r\n\r\n")]
    [InlineData(BlankLinePolicy.Remove, "\n\n", "\r\n")]
    [InlineData(BlankLinePolicy.Preserve, "\n\n", "\r\n\r\n")]
    [InlineData(BlankLinePolicy.Preserve, "\n", "\r\n")]
    public void ConfiguresBlankLinesAfterExistingMultilineCalls(BlankLinePolicy policy, string sourceGap, string expectedGap)
    {
        const string call = "Run(1,\n    2);";
        AssertFormatted(call + sourceGap + "Done();",
            call.Replace("\n", "\r\n", StringComparison.Ordinal) + expectedGap + "Done();\r\n", Options(policy));
    }

    [Theory]
    [InlineData("_axis.Move")]
    [InlineData("THIS^.Move")]
    [InlineData("SUPER^.Move")]
    [InlineData("_axes[index + 1].Move")]
    [InlineData("_axes[GetIndex(1)].Move")]
    public void RecognizesQualifiedAndIndexedCalls(string target)
    {
        var call = target + "(1,\n    2); // keep";
        AssertFormatted(call + "\nDone();",
            call.Replace("\n", "\r\n", StringComparison.Ordinal) + "\r\n\r\nDone();\r\n",
            Options(BlankLinePolicy.Require));
    }

    [Fact]
    public void AddsBlankLineAfterNewlyWrappedCall()
    {
        var options = Options(BlankLinePolicy.Require) with
        {
            Wrapping = Options(BlankLinePolicy.Require).Wrapping with { Calls = WrapStyle.Always }
        };
        AssertFormatted("Run(1, 2);\nDone();",
            "Run(\r\n    1,\r\n    2\r\n);\r\n\r\nDone();\r\n", options);
    }

    [Fact]
    public void AddsBlankLineOnlyAfterTheEntireOuterCall()
    {
        AssertFormatted("Run(Build(1,\n2),\n3); (* keep *)\nDone();",
            "Run(Build(1,\r\n    2),\r\n    3); (* keep *)\r\n\r\nDone();\r\n",
            Options(BlankLinePolicy.Require));
    }

    [Theory]
    [InlineData("value := CONCAT('a',\n'b');", "value := CONCAT('a',\r\n    'b');")]
    [InlineData("value : STRING := CONCAT('a',\n'b');", "value : STRING := CONCAT('a',\r\n    'b');")]
    [InlineData("values := [1,\n2];", "values := [1,\r\n    2];")]
    [InlineData("Run();", "Run();")]
    public void DoesNotSeparateEmbeddedCallsInitializersOrShortCalls(string source, string expected)
    {
        AssertFormatted(source + "\nDone();", expected + "\r\nDone();\r\n", Options(BlankLinePolicy.Require));
    }

    [Fact]
    public void ClosingKeywordRemovalAndGlobalBlankLineLimitTakePrecedence()
    {
        AssertFormatted("IF ready THEN\nRun(1,\n2);\nEND_IF",
            "IF ready THEN\r\n    Run(1,\r\n        2);\r\nEND_IF\r\n", Options(BlankLinePolicy.Require));
        var options = Options(BlankLinePolicy.Require) with
        {
            Layout = Options(BlankLinePolicy.Require).Layout with { MaximumConsecutiveBlankLines = 0 }
        };
        AssertFormatted("Run(1,\n2);\nDone();", "Run(1,\r\n    2);\r\nDone();\r\n", options);
    }

    [Fact]
    public void DoesNotAddBlankLinesAtEndOfFileOrWithinPreservedStatementLines()
    {
        AssertFormatted("Run(1,\n2);", "Run(1,\r\n    2);\r\n", Options(BlankLinePolicy.Require));
        var options = Options(BlankLinePolicy.Require) with
        {
            Layout = Options(BlankLinePolicy.Require).Layout with { OneStatementPerLine = false }
        };
        AssertFormatted("Run(1,\n2); Done();\nFinish();",
            "Run(1,\r\n    2); Done();\r\nFinish();\r\n", options);
    }

    private static FormatterOptions Options(BlankLinePolicy policy) => FormatterOptions.Default with
    {
        Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 0 },
        Wrapping = FormatterOptions.Default.Wrapping with
        {
            Calls = WrapStyle.Preserve,
            Initializers = WrapStyle.Preserve,
            BinaryExpressions = WrapStyle.Preserve
        },
        BlankLines = FormatterOptions.Default.BlankLines with { AfterMultilineCall = policy }
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
