using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class ExpandedArgumentTests
{
    [Theory]
    [InlineData(BinaryOperatorPosition.Before, "    100.0 * velocity\r\n    / TO_LREAL(command)")]
    [InlineData(BinaryOperatorPosition.After, "    100.0 * velocity /\r\n    TO_LREAL(command)")]
    public void ExpandsCalculationAndPositionsExistingDivision(BinaryOperatorPosition position, string expression)
    {
        AssertFormatted("pressureSpeedOverride := LREAL_TO_INT(100.0 * velocity /\nTO_LREAL(command));",
            "pressureSpeedOverride := LREAL_TO_INT(\r\n" + expression + "\r\n);\r\n",
            Options() with { Wrapping = Options().Wrapping with { BinaryOperatorPosition = position } });
    }

    [Fact]
    public void KeepsSimpleArgumentsHanging()
    {
        AssertFormatted("Move(100,\n20);", "Move(100,\r\n     20);\r\n", Options());
    }

    [Fact]
    public void CanDisableExpansion()
    {
        AssertFormatted("value := Convert(first /\nsecond);", "value := Convert(first /\r\n    second);\r\n",
            Options() with { Wrapping = Options().Wrapping with { ExpandMultilineArguments = false } });
    }

    [Fact]
    public void ExpandsArgumentsThatBecomeMultilineThroughWrapping()
    {
        AssertFormatted("value := Convert(first + second);", "value := Convert(\r\n    first\r\n    + second\r\n);\r\n",
            Options() with { Wrapping = Options().Wrapping with { BinaryExpressions = WrapStyle.Always } });
    }

    [Fact]
    public void IndentsNestedExpandedCalls()
    {
        AssertFormatted("IF ready THEN\nvalue := Outer(Inner(first /\nsecond), third);\nEND_IF",
            "IF ready THEN\r\n    value := Outer(\r\n        Inner(\r\n            first\r\n            / second\r\n        ),\r\n        third\r\n    );\r\nEND_IF\r\n",
            Options());
    }

    [Fact]
    public void SupportsTabsAndPreservedCallWrapping()
    {
        AssertFormatted("IF ready THEN\nvalue := Convert(first /\nsecond);\nEND_IF",
            "IF ready THEN\r\n\tvalue := Convert(\r\n\t\tfirst\r\n\t\t/ second\r\n\t);\r\nEND_IF\r\n",
            Options() with
            {
                Indentation = Options().Indentation with { Style = IndentStyle.Tabs },
                Wrapping = Options().Wrapping with { Calls = WrapStyle.Preserve, Initializers = WrapStyle.Preserve }
            });
    }

    [Fact]
    public void KeepsNestedSimpleArgumentsHanging()
    {
        AssertFormatted("value := Outer(Inner(first,\nsecond), third);",
            "value := Outer(\r\n    Inner(first,\r\n          second),\r\n    third\r\n);\r\n", Options());
    }

    [Theory]
    [InlineData("value := Convert(first / // divisor\nsecond);",
        "value := Convert(\r\n    first / // divisor\r\n    second\r\n);\r\n")]
    [InlineData("value := Convert(first /\n(* divisor *) second);",
        "value := Convert(\r\n    first /\r\n    (* divisor *) second\r\n);\r\n")]
    [InlineData("value := Convert(first /\n-second);",
        "value := Convert(\r\n    first\r\n    / -second\r\n);\r\n")]
    [InlineData("value := Convert(first -\nsecond);",
        "value := Convert(\r\n    first\r\n    - second\r\n);\r\n")]
    public void PreservesCommentsAndUnarySigns(string source, string expected)
    {
        AssertFormatted(source, expected, Options());
    }

    [Fact]
    public void MovesExistingLeadingOperatorToTrailingPosition()
    {
        AssertFormatted("value := Convert(first\n- second);", "value := Convert(\r\n    first -\r\n    second\r\n);\r\n",
            Options() with { Wrapping = Options().Wrapping with { BinaryOperatorPosition = BinaryOperatorPosition.After } });
    }

    [Fact]
    public void KeepsShortCallsAndStringContentsIntact()
    {
        AssertFormatted("value := Convert('first / second');", "value := Convert('first / second');\r\n", Options());
    }

    private static FormatterOptions Options() => FormatterOptions.Default with
    {
        Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 0 },
        Wrapping = FormatterOptions.Default.Wrapping with
        {
            ExpandMultilineArguments = true,
            BinaryExpressions = WrapStyle.Preserve
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
