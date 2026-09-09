using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class ClosingDelimiterTests
{
    [Theory]
    [InlineData(ClosingDelimiterStyle.OwnLine, "\r\n")]
    [InlineData(ClosingDelimiterStyle.SameLine, "")]
    [InlineData(ClosingDelimiterStyle.Preserve, "\r\n")]
    public void ControlsStructureAndArrayInitializers(ClosingDelimiterStyle style, string gap)
    {
        var options = Options(style) with { Wrapping = Options(style).Wrapping with { Initializers = WrapStyle.Always } };
        AssertFormatted("monitor : FB_Monitor := (Name := 'monitor', Reset := reset);",
            "monitor : FB_Monitor := (\r\n    Name := 'monitor',\r\n    Reset := reset" + gap + ");\r\n", options);
        AssertFormatted("sensors : ARRAY[0..1] OF FB_Sensor := [(Name := 'one'), (Name := 'two')];",
            "sensors : ARRAY[0..1] OF FB_Sensor := [\r\n    (Name := 'one'),\r\n    (Name := 'two')" + gap + "];\r\n", options);
    }

    [Theory]
    [InlineData(ClosingDelimiterStyle.OwnLine, "Call(first,\r\n    second\r\n);\r\n")]
    [InlineData(ClosingDelimiterStyle.SameLine, "Call(first,\r\n    second);\r\n")]
    public void OverridesPreservedMultilineCalls(ClosingDelimiterStyle style, string expected)
    {
        AssertFormatted("Call(first,\nsecond\n);", expected, Options(style));
    }

    [Fact]
    public void ControlsParenthesesAndBracketsIndependently()
    {
        var options = Options(ClosingDelimiterStyle.OwnLine) with
        {
            Wrapping = Options(ClosingDelimiterStyle.OwnLine).Wrapping with { MultilineClosingBracket = ClosingDelimiterStyle.SameLine }
        };
        AssertFormatted("values := [Call(first,\nsecond\n)\n];",
            "values := [Call(first,\r\n    second\r\n)];\r\n", options);
    }

    [Theory]
    [InlineData(ClosingDelimiterStyle.OwnLine)]
    [InlineData(ClosingDelimiterStyle.SameLine)]
    public void KeepsSingleLineExpressionsAndLiteralContents(ClosingDelimiterStyle style)
    {
        AssertFormatted("value := Call(')]', values[0]);", "value := Call(')]', values[0]);\r\n", Options(style));
    }

    [Fact]
    public void SameLineKeepsClosingDelimiterOutOfLineComments()
    {
        AssertFormatted("Call(first,\nsecond // keep\n);",
            "Call(first,\r\n    second // keep\r\n);\r\n", Options(ClosingDelimiterStyle.SameLine));
    }

    [Fact]
    public void SameLineRespectsSpacesInsideDelimiters()
    {
        var options = Options(ClosingDelimiterStyle.SameLine) with
        {
            Spacing = FormatterOptions.Default.Spacing with { InsideParentheses = true, InsideBrackets = true }
        };
        AssertFormatted("Call(first,\nsecond\n);", "Call( first,\r\n    second );\r\n", options);
        AssertFormatted("values := [first,\nsecond\n];", "values := [ first,\r\n    second ];\r\n", options);
    }

    [Fact]
    public void SameLineWorksWithExpandedArgumentsAndTabs()
    {
        var options = Options(ClosingDelimiterStyle.SameLine) with
        {
            Indentation = FormatterOptions.Default.Indentation with { Style = IndentStyle.Tabs },
            Wrapping = Options(ClosingDelimiterStyle.SameLine).Wrapping with { ExpandMultilineArguments = true }
        };
        AssertFormatted("IF ready THEN\nvalue := Convert(first /\nsecond);\nEND_IF",
            "IF ready THEN\r\n\tvalue := Convert(\r\n\t\tfirst\r\n\t\t/ second);\r\nEND_IF\r\n", options);
    }

    private static FormatterOptions Options(ClosingDelimiterStyle style) => FormatterOptions.Default with
    {
        Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 0 },
        Alignment = FormatterOptions.Default.Alignment with { NamedInputs = false },
        Wrapping = FormatterOptions.Default.Wrapping with
        {
            Calls = WrapStyle.Preserve,
            Initializers = WrapStyle.Preserve,
            BinaryExpressions = WrapStyle.Preserve,
            MultilineClosingParenthesis = style,
            MultilineClosingBracket = style
        }
    };

    private static void AssertFormatted(string source, string expected, FormatterOptions options)
    {
        var result = StructuredTextFormatter.Format(source, options);
        Assert.True(result.IsValid, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(expected, result.FormattedText);
        var second = StructuredTextFormatter.Format(result.FormattedText, options);
        Assert.True(second.IsValid);
        Assert.Equal(result.FormattedText, second.FormattedText);
    }
}
