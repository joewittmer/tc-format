using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class CompactControlHeaderTests
{
    [Fact]
    public void FormatsPreferredGroupedConditionWithBlockCallArguments()
    {
        const string source = "IF NOT _clampingBeam.Busy AND\n(\n_machineMode <> E_Machine_Mode.Automatic\n" +
                              "OR NOT _backgauge.IsExecuteGroupBusy(area := E_Backgauge_Area.Clamps,\nasyncCommandGroup := groupOne)\n)\nTHEN\nRun();\nEND_IF";
        const string expected = "IF NOT _clampingBeam.Busy AND\r\n" +
                                "    ( _machineMode <> E_Machine_Mode.Automatic OR\r\n" +
                                "        NOT _backgauge.IsExecuteGroupBusy(\r\n" +
                                "            area := E_Backgauge_Area.Clamps,\r\n" +
                                "            asyncCommandGroup := groupOne)) THEN\r\n    Run();\r\nEND_IF\r\n";
        AssertFormatted(source, expected);
    }

    [Theory]
    [InlineData("IF", "THEN", "END_IF")]
    [InlineData("WHILE", "DO", "END_WHILE")]
    public void KeepsShortCallsInlineAndJoinsTheHeaderTerminator(string opening, string terminator, string closing)
    {
        AssertFormatted($"{opening} first AND\nCheck(value)\n{terminator}\nRun();\n{closing}",
            $"{opening} first AND\r\n    Check(value) {terminator}\r\n    Run();\r\n{closing}\r\n");
    }

    [Fact]
    public void CommentsPreventJoiningOrMovingOperatorsAcrossThem()
    {
        AssertFormatted("IF first // keep\nAND Check(\na,\nb // value\n)\nTHEN\nEND_IF",
            "IF first // keep\r\n    AND Check(\r\n        a,\r\n        b // value\r\n    ) THEN\r\nEND_IF\r\n");
        AssertFormatted("IF ready // reason\nTHEN\nEND_IF",
            "IF ready // reason\r\nTHEN\r\nEND_IF\r\n");
    }

    [Fact]
    public void RespectsTabsAndNestedControlFlow()
    {
        AssertFormatted("IF ready THEN\nIF first AND\n(\nsecond OR\nCheck(a,\nb)) THEN\nRun();\nEND_IF\nEND_IF",
            "IF ready THEN\r\n\tIF first AND\r\n\t\t( second OR\r\n\t\t\tCheck(\r\n\t\t\t\ta,\r\n\t\t\t\tb)) THEN\r\n\t\tRun();\r\n\tEND_IF\r\nEND_IF\r\n",
            Options with { Indentation = Options.Indentation with { Style = IndentStyle.Tabs } });
    }

    [Fact]
    public void DoesNotApplyCompactHeaderRulesToAssignmentsOrSingleLineHeaders()
    {
        AssertFormatted("value := Call(a,\nb);\nIF (ready) AND Check(value) THEN\nEND_IF",
            "value := Call(a,\r\n              b);\r\nIF (ready) AND Check(value) THEN\r\nEND_IF\r\n");
    }

    private static FormatterOptions Options => FormatterOptions.Default with
    {
        Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 0 },
        Alignment = new AlignmentOptions(false, false, false, false, false, false, false),
        Wrapping = FormatterOptions.Default.Wrapping with
        {
            Calls = WrapStyle.Hanging,
            Initializers = WrapStyle.Preserve,
            BinaryExpressions = WrapStyle.Preserve,
            CompactControlFlowHeaders = true,
            MultilineClosingParenthesis = ClosingDelimiterStyle.SameLine
        }
    };

    private static void AssertFormatted(string source, string expected, FormatterOptions? options = null)
    {
        options ??= Options;
        var first = StructuredTextFormatter.Format(source, options);
        Assert.True(first.IsValid, string.Join("; ", first.Diagnostics.Select(d => d.Message)));
        Assert.Equal(expected, first.FormattedText);
        var second = StructuredTextFormatter.Format(first.FormattedText, options);
        Assert.True(second.IsValid);
        Assert.Equal(first.FormattedText, second.FormattedText);
    }
}
