using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class NestedInitializerTests
{
    [Theory]
    [InlineData(ClosingDelimiterStyle.SameLine)]
    [InlineData(ClosingDelimiterStyle.OwnLine)]
    public void IndentsArraysInsideStructureAfterConstructor(ClosingDelimiterStyle closing)
    {
        const string source = "VAR\nbeam : FB_Beam(logger := logger,\nisUpperBeam := TRUE) := (\n" +
                              "Name := 'Upper Bending Beam',\nWedgeAxisNames := [\n'Axis 1',\n'Axis 2'],\n" +
                              "HookAxisNames := ['Hook 1', 'Hook 2']);\nEND_VAR";
        var options = Options(closing);
        var expected = closing == ClosingDelimiterStyle.SameLine
            ? "VAR\n    beam : FB_Beam(logger := logger,\n                   isUpperBeam := TRUE) := (\n" +
              "        Name := 'Upper Bending Beam',\n        WedgeAxisNames := [\n            'Axis 1',\n            'Axis 2'],\n" +
              "        HookAxisNames := [\n            'Hook 1',\n            'Hook 2']);\nEND_VAR\n"
            : "VAR\n    beam : FB_Beam(logger := logger,\n                   isUpperBeam := TRUE\n    ) := (\n" +
              "        Name := 'Upper Bending Beam',\n        WedgeAxisNames := [\n            'Axis 1',\n            'Axis 2'\n        ],\n" +
              "        HookAxisNames := [\n            'Hook 1',\n            'Hook 2'\n        ]\n    );\nEND_VAR\n";

        AssertFormatted(source, expected, options);
    }

    [Theory]
    [InlineData(WrapStyle.Always)]
    [InlineData(WrapStyle.WhenLong)]
    [InlineData(WrapStyle.Preserve)]
    public void IndentsExistingNestedInitializersWithWrappingDisabledOrEnabled(WrapStyle style)
    {
        var options = Options(ClosingDelimiterStyle.Preserve) with
        {
            Wrapping = Options(ClosingDelimiterStyle.Preserve).Wrapping with { Initializers = style, Calls = WrapStyle.Preserve }
        };
        const string source = "value := (\nItems := [\n[\n1,\n2\n],\n[\n3,\n4\n]\n],\nEnabled := TRUE\n);";
        const string expected = "value := (\n    Items := [\n        [\n            1,\n            2\n        ],\n" +
                                "        [\n            3,\n            4\n        ]\n    ],\n    Enabled := TRUE\n);\n";
        AssertFormatted(source, expected, options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndentsArrayStructureMembersAndPreservesComments(bool expandArguments)
    {
        var options = Options(ClosingDelimiterStyle.SameLine) with
        {
            Wrapping = Options(ClosingDelimiterStyle.SameLine).Wrapping with { ExpandMultilineArguments = expandArguments }
        };
        AssertFormatted("values := [\n(Name := 'one',\nAxes := [\n'first', // keep\n// second axis\n'second'\n]),\n" +
                        "(Name := 'two',\nAxes := ['third'])];",
            "values := [\n    (Name := 'one',\n        Axes := [\n            'first', // keep\n            // second axis\n" +
            "            'second']),\n    (Name := 'two',\n        Axes := [\n            'third'])];\n", options);
    }

    [Theory]
    [InlineData(4, "\t")]
    [InlineData(2, "  ")]
    public void UsesConfiguredContinuationIndentationInsideControlBlocks(int continuationSize, string continuation)
    {
        var options = Options(ClosingDelimiterStyle.OwnLine) with
        {
            Indentation = FormatterOptions.Default.Indentation with { Style = IndentStyle.Tabs, ContinuationSize = continuationSize }
        };
        AssertFormatted("IF ready THEN\nvalue := (Items := [1, 2]);\nEND_IF",
            "IF ready THEN\n\tvalue := (\n\t" + continuation + "Items := [\n\t" + continuation + continuation +
            "1,\n\t" + continuation + continuation + "2\n\t" + continuation + "]\n\t);\nEND_IF\n", options);
    }

    [Fact]
    public void KeepsNestedHangingInitializersAlignedToTheirFirstItems()
    {
        var options = Options(ClosingDelimiterStyle.Preserve) with
        {
            Wrapping = Options(ClosingDelimiterStyle.Preserve).Wrapping with { Initializers = WrapStyle.Hanging }
        };
        AssertFormatted("value := (Items := [first,\nsecond],\nEnabled := TRUE);",
            "value := (Items := [first,\n                    second],\n          Enabled := TRUE);\n", options);
    }

    [Fact]
    public void IndentsExpandedCallsInsideNestedInitializers()
    {
        var options = Options(ClosingDelimiterStyle.OwnLine) with
        {
            Wrapping = Options(ClosingDelimiterStyle.OwnLine).Wrapping with { ExpandMultilineArguments = true }
        };
        AssertFormatted("value := (Items := [Convert(first +\nsecond), 2]);",
            "value := (\n    Items := [\n        Convert(\n            first\n            + second\n        ),\n" +
            "        2\n    ]\n);\n", options);
    }

    [Fact]
    public void AlignsMultipleLeadingClosingsWithTheOutermostClosedBlock()
    {
        var options = Options(ClosingDelimiterStyle.Preserve) with
        {
            Wrapping = Options(ClosingDelimiterStyle.Preserve).Wrapping with { Initializers = WrapStyle.Preserve }
        };
        AssertFormatted("value := (Items := [\n1\n]);", "value := (Items := [\n    1\n]);\n", options);
    }

    private static FormatterOptions Options(ClosingDelimiterStyle closing) => FormatterOptions.Default with
    {
        File = FormatterOptions.Default.File with { EndOfLine = EndOfLineStyle.Lf },
        Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 0 },
        Alignment = FormatterOptions.Default.Alignment with { NamedInputs = false },
        Wrapping = FormatterOptions.Default.Wrapping with
        {
            Initializers = WrapStyle.Always,
            BinaryExpressions = WrapStyle.Preserve,
            MultilineClosingParenthesis = closing,
            MultilineClosingBracket = closing
        }
    };

    private static void AssertFormatted(string source, string expected, FormatterOptions options)
    {
        var first = StructuredTextFormatter.Format(source, options);
        Assert.True(first.IsValid, string.Join("; ", first.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(expected, first.FormattedText);
        var second = StructuredTextFormatter.Format(first.FormattedText, options);
        Assert.True(second.IsValid);
        Assert.False(second.Changed);
    }
}
