using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class MultilineAssignmentLayoutTests
{
    [Theory]
    [InlineData(":=")]
    [InlineData("REF=")]
    [InlineData("?=")]
    public void AlignsUnderFirstExpressionAndSeparatesOnlyMultilineAssignments(string op)
    {
        var indent = new string(' ', "stopped ".Length + op.Length + 1);
        AssertFormatted($"first := 1;\nsecond := 2;\nstopped {op} NOT clamp.Busy AND\nNOT runner.Busy AND\nNOT backgauge.Busy;\nlast := 3;\nnext := 4;",
            $"first := 1;\r\nsecond := 2;\r\n\r\nstopped {op} NOT clamp.Busy AND\r\n{indent}NOT runner.Busy AND\r\n{indent}NOT backgauge.Busy;\r\n\r\nlast := 3;\r\nnext := 4;\r\n");
    }

    [Theory]
    [InlineData(IndentStyle.Spaces, "    ", "        ", "               ")]
    [InlineData(IndentStyle.Tabs, "\t", "\t\t", "\t           ")]
    public void RhsOnOwnLineUsesContinuationIndentWhileSameLineRhsHangs(
        IndentStyle style, string block, string continuation, string hanging)
    {
        var options = Options with { Indentation = Options.Indentation with { Style = style } };
        AssertFormatted("IF ready THEN\nresult :=\nfirst OR\nsecond;\nother := TRUE;\nstopped := first OR\nsecond;\nEND_IF",
            $"IF ready THEN\r\n{block}result :=\r\n{continuation}first OR\r\n{continuation}second;\r\n\r\n{block}other := TRUE;\r\n\r\n{block}stopped := first OR\r\n{hanging}second;\r\nEND_IF\r\n", options);
    }

    [Theory]
    [InlineData(BlankLinePolicy.Require, "\n", "\r\n\r\n")]
    [InlineData(BlankLinePolicy.Remove, "\n\n", "\r\n")]
    [InlineData(BlankLinePolicy.Preserve, "\n\n", "\r\n\r\n")]
    public void ConfiguresBeforeAndAfterSeparators(BlankLinePolicy policy, string inputGap, string outputGap)
    {
        var options = Options with
        {
            BlankLines = Options.BlankLines with { BeforeMultilineAssignment = policy, AfterMultilineAssignment = policy }
        };
        AssertFormatted("Prepare();" + inputGap + "a := b OR\nc;" + inputGap + "Done();",
            "Prepare();" + outputGap + "a := b OR\r\n     c;" + outputGap + "Done();\r\n", options);
    }

    [Fact]
    public void KeepsContinuationCommentsTogetherAndPreservesSingleLineGrouping()
    {
        AssertFormatted("a := 1;\n\nb := 2;\nx := first OR\n\n// reason\nsecond OR\n(* note *)\nthird;\nz := 3;",
            "a := 1;\r\n\r\nb := 2;\r\n\r\nx := first OR\r\n     // reason\r\n     second OR\r\n     (* note *)\r\n     third;\r\n\r\nz := 3;\r\n");
    }

    [Fact]
    public void ShiftsNestedCallsWithTheirContinuationLine()
    {
        AssertFormatted("result := first OR\nCheck(a,\nb) OR\nlast;",
            "result := first OR\r\n          Check(a,\r\n                b) OR\r\n          last;\r\n",
            Options with { Wrapping = Options.Wrapping with { Calls = WrapStyle.Hanging } });
    }

    [Fact]
    public void KeepsExistingCallAndInitializerLayouts()
    {
        AssertFormatted("result := Call(a,\nb);\nvalues := [1,\n2];",
            "result := Call(a,\r\n               b);\r\n\r\nvalues := [1,\r\n    2];\r\n",
            Options with { Wrapping = Options.Wrapping with { Calls = WrapStyle.Hanging } });
    }

    [Fact]
    public void AppliesSeparatorsAfterWrappingAndHonorsGlobalLimit()
    {
        var options = Options with { Wrapping = Options.Wrapping with { BinaryExpressions = WrapStyle.Always } };
        AssertFormatted("Prepare();\nx := a OR b;\nDone();",
            "Prepare();\r\n\r\nx := a\r\n     OR b;\r\n\r\nDone();\r\n", options);
        options = options with { Layout = options.Layout with { MaximumConsecutiveBlankLines = 0 } };
        AssertFormatted("Prepare();\nx := a OR b;\nDone();",
            "Prepare();\r\nx := a\r\n     OR b;\r\nDone();\r\n", options);
    }

    [Fact]
    public void DoesNotAddSeparatorsWhenClosingDelimiterJoiningMakesAssignmentSingleLine()
    {
        AssertFormatted("Prepare();\nx := Call(1\n);\nDone();",
            "Prepare();\r\nx := Call(1);\r\nDone();\r\n",
            Options with { Wrapping = Options.Wrapping with { MultilineClosingParenthesis = ClosingDelimiterStyle.SameLine } });
    }

    [Fact]
    public void InlineBlockCommentDoesNotHideTheFirstExpression()
    {
        AssertFormatted("x := (* reason *) first OR\nsecond;",
            "x := (* reason *) first OR\r\n                  second;\r\n");
    }

    [Fact]
    public void ExcludesDeclarationInitializersNamedArgumentsAndForHeaders()
    {
        AssertFormatted("VAR\nx : BOOL := first OR\nsecond;\ny : BOOL;\nEND_VAR\nFOR i :=\n0 TO 2 DO\nCall(input := first OR\nsecond);\nRun();\nEND_FOR",
            "VAR\r\n    x : BOOL := first OR\r\n        second;\r\n    y : BOOL;\r\nEND_VAR\r\nFOR i :=\r\n    0 TO 2 DO\r\n    Call(input := first OR\r\n        second);\r\n    Run();\r\nEND_FOR\r\n");
    }

    [Fact]
    public void UsesFinalAssignmentColumnAfterVerticalAlignment()
    {
        AssertFormatted("longName := 1;\nx := first OR\nsecond;",
            "longName := 1;\r\nx        := first OR\r\n            second;\r\n",
            Options with
            {
                Alignment = Options.Alignment with { Assignments = true },
                BlankLines = Options.BlankLines with { BeforeMultilineAssignment = BlankLinePolicy.Preserve }
            });
    }

    [Fact]
    public void DoesNotTreatSplitHeadersOrSplitDeclarationsAsAssignments()
    {
        AssertFormatted("VAR\nx :\nCustomType := first OR\nsecond;\nEND_VAR\nFOR\ni := 0\nTO 2 DO\nRun();\nEND_FOR",
            "VAR\r\n    x:\r\n    CustomType := first OR\r\n        second;\r\nEND_VAR\r\nFOR\r\n    i := 0\r\n    TO 2 DO\r\n    Run();\r\nEND_FOR\r\n");
    }

    [Fact]
    public void PreservesMultilineCommentTextAndKeepsLeadingCommentsAttached()
    {
        AssertFormatted("Prepare();\n// explain assignment\nx := first OR\n(* keep\n  exact text *)\nsecond;\nDone();",
            "Prepare();\r\n// explain assignment\r\nx := first OR\r\n(* keep\n  exact text *)\r\n     second;\r\n\r\nDone();\r\n",
            Options with { BlankLines = Options.BlankLines with { AfterComment = BlankLinePolicy.Remove } });
    }

    [Fact]
    public void PreservedAdjacentStatementsDoNotCreateInteriorBoundaries()
    {
        AssertFormatted("Prepare(); x := first OR\nsecond; Done();\nFinish();",
            "Prepare(); x := first OR\r\n                second; Done();\r\nFinish();\r\n",
            Options with { Layout = Options.Layout with { OneStatementPerLine = false } });
    }

    private static FormatterOptions Options => FormatterOptions.Default with
    {
        Alignment = new AlignmentOptions(false, false, false, false, false, false, false),
        Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 0 },
        Wrapping = FormatterOptions.Default.Wrapping with
        {
            Calls = WrapStyle.Preserve,
            Initializers = WrapStyle.Preserve,
            BinaryExpressions = WrapStyle.Preserve
        },
        BlankLines = FormatterOptions.Default.BlankLines with
        {
            BeforeMultilineAssignment = BlankLinePolicy.Require,
            AfterMultilineAssignment = BlankLinePolicy.Require
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
