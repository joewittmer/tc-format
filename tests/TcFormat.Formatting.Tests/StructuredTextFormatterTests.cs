using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class StructuredTextFormatterTests
{
    [Fact]
    public void FormatsKeywordsLineEndingsTrailingWhitespaceAndBlankLines()
    {
        const string source = "program Main  \n\n\nvar\n    value : int;\t\nend_var\nend_program";

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Equal(
            "PROGRAM Main\r\n\r\nVAR\r\n    value : INT;\r\nEND_VAR\r\nEND_PROGRAM\r\n",
            result.FormattedText);
    }

    [Fact]
    public void PreservesCommentAndLiteralContents()
    {
        const string source = "// lower if\nvalue := 'lower if';\n(* lower then *)";

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Contains("// lower if", result.FormattedText, StringComparison.Ordinal);
        Assert.Contains("'lower if'", result.FormattedText, StringComparison.Ordinal);
        Assert.Contains("(* lower then *)", result.FormattedText, StringComparison.Ordinal);
    }

    [Fact]
    public void PreserveKeywordCaseLeavesKeywordsUnchanged()
    {
        var options = FormatterOptions.Default with { KeywordCase = KeywordCase.Preserve };

        var result = StructuredTextFormatter.Format("program Main\nend_program", options);

        Assert.Equal("program Main\r\nend_program\r\n", result.FormattedText);
    }

    [Fact]
    public void FormattingIsIdempotent()
    {
        const string source = "if enabled then  \nvalue:=1;\nend_if";

        var first = StructuredTextFormatter.Format(source, FormatterOptions.Default);
        var second = StructuredTextFormatter.Format(first.FormattedText, FormatterOptions.Default);

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.Equal(first.FormattedText, second.FormattedText);
        Assert.False(second.Changed);
    }

    [Fact]
    public void InvalidSourceIsNotChanged()
    {
        const string source = "value := 'unterminated";

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.False(result.IsValid);
        Assert.Equal(source, result.FormattedText);
    }

    [Fact]
    public void FinalNewlineCanBeDisabled()
    {
        var options = FormatterOptions.Default with
        {
            File = FormatterOptions.Default.File with { InsertFinalNewline = false }
        };

        var result = StructuredTextFormatter.Format("VAR\nEND_VAR\n\n", options);

        Assert.Equal("VAR\r\nEND_VAR", result.FormattedText);
    }

    [Fact]
    public void IndentsNestedControlFlowCaseBranchesAndContinuations()
    {
        const string source =
            """
            var
            value : int;
            end_var
            if enabled then
            case mode of
            1:
            fbRun(
            execute := true,
            done => complete);
            2:
            value := 2;
            else
            value := 0;
            end_case
            end_if
            """;

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Equal(
            "VAR\r\n" +
            "    value : INT;\r\n" +
            "END_VAR\r\n" +
            "IF enabled THEN\r\n" +
            "    CASE mode OF\r\n" +
            "        1:\r\n" +
            "            fbRun(\r\n" +
            "                execute := TRUE,\r\n" +
            "                done" + new string(' ', 4) + "=> complete);\r\n" +
            "        2:\r\n" +
            "            value := 2;\r\n\r\n" +
            "        ELSE\r\n" +
            "            value := 0;\r\n" +
            "    END_CASE\r\n" +
            "END_IF\r\n",
            result.FormattedText);
    }

    [Fact]
    public void TabIndentationUsesTabsForBlocksAndContinuations()
    {
        var options = FormatterOptions.Default with
        {
            Indentation = FormatterOptions.Default.Indentation with { Style = IndentStyle.Tabs }
        };

        var result = StructuredTextFormatter.Format("IF x THEN\ny := 1;\nEND_IF", options);

        Assert.Equal("IF x THEN\r\n\ty := 1;\r\nEND_IF\r\n", result.FormattedText);
    }

    [Fact]
    public void MismatchedBlockIsRejectedWithoutChanges()
    {
        const string source = "IF x THEN\ny := 1;\nEND_CASE";

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.False(result.IsValid);
        Assert.Equal(source, result.FormattedText);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("Unexpected END_CASE"));
    }

    [Fact]
    public void AppliesConfiguredTokenSpacing()
    {
        const string source =
            """
            VAR
            value:INT:=1;
            END_VAR
            IF(value>=1)AND NOT done THEN
            fbRun(execute:=TRUE,done=>complete);
            slice:=values[1 .. 10];
            signedValue:=-1;
            END_IF
            """;

        var options = FormatterOptions.Default with
        {
            Alignment = FormatterOptions.Default.Alignment with { Assignments = false }
        };
        var result = StructuredTextFormatter.Format(source, options);

        Assert.True(result.IsValid);
        Assert.Equal(
            "VAR\r\n" +
            "    value : INT := 1;\r\n" +
            "END_VAR\r\n" +
            "IF(value >= 1) AND NOT done THEN\r\n" +
            "    fbRun(execute := TRUE, done => complete);\r\n" +
            "    slice := values[1..10];\r\n" +
            "    signedValue := -1;\r\n" +
            "END_IF\r\n",
            result.FormattedText);
    }

    [Fact]
    public void SpacingCanBeDisabledSelectively()
    {
        var options = FormatterOptions.Default with
        {
            Spacing = FormatterOptions.Default.Spacing with
            {
                BeforeDeclarationColon = false,
                AfterDeclarationColon = false,
                AroundAssignmentOperators = false,
                AroundComparisonOperators = false,
                AfterComma = false
            }
        };

        var result = StructuredTextFormatter.Format("VAR\nx : INT := 1;\nEND_VAR\nx = 1;", options);

        Assert.Equal("VAR\r\n    x:INT:=1;\r\nEND_VAR\r\nx=1;\r\n", result.FormattedText);
    }

    [Fact]
    public void AlignsDeclarationColonsInitializersAddressesAndAssignments()
    {
        const string source =
            """
            VAR
            x : INT := 1;
            longerName : DINT := 2;
            input AT %IX0.0 : BOOL;
            longerInput AT %IX0.1 : BOOL;
            END_VAR

            x := 2;
            longerName := 3;
            """;

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Equal(
            "VAR\r\n" +
            "    x" + new string(' ', 21) + ": INT  := 1;\r\n" +
            "    longerName" + new string(' ', 12) + ": DINT := 2;\r\n" +
            "    input" + new string(' ', 7) + "AT %IX0.0 : BOOL;\r\n" +
            "    longerInput AT %IX0.1 : BOOL;\r\n" +
            "END_VAR\r\n" +
            "\r\n" +
            "x          := 2;\r\n" +
            "longerName := 3;\r\n",
            result.FormattedText);
    }

    [Fact]
    public void AlignmentCanBeDisabled()
    {
        var options = FormatterOptions.Default with
        {
            Alignment = FormatterOptions.Default.Alignment with
            {
                Declarations = false,
                DeclarationInitializers = false,
                Assignments = false,
                Addresses = false
            }
        };

        var result = StructuredTextFormatter.Format(
            "VAR\nx : INT := 1;\nlonger : DINT := 2;\nEND_VAR\nx := 1;\nlonger := 2;",
            options);

        Assert.Equal(
            "VAR\r\n    x : INT := 1;\r\n    longer : DINT := 2;\r\nEND_VAR\r\nx := 1;\r\nlonger := 2;\r\n",
            result.FormattedText);
    }

    [Fact]
    public void AlignmentIsSkippedWhenItWouldExceedMaximumLineLength()
    {
        var options = FormatterOptions.Default with
        {
            Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 24 }
        };

        var result = StructuredTextFormatter.Format(
            "short := 1;\nveryLongVariableName := 2;",
            options);

        Assert.Equal("short := 1;\r\nveryLongVariableName := 2;\r\n", result.FormattedText);
    }

    [Fact]
    public void AlignsNamedInputsAndOutputsWithinTheSameCall()
    {
        const string source =
            "fbRun(\n" +
            "short := TRUE,\n" +
            "muchLongerInput := 1,\n" +
            "done => complete,\n" +
            "isRunning => running);";

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Equal(
            "fbRun(\r\n" +
            "    short" + new string(' ', 11) + ":= TRUE,\r\n" +
            "    muchLongerInput := 1,\r\n" +
            "    done" + new string(' ', 12) + "=> complete,\r\n" +
            "    isRunning" + new string(' ', 7) + "=> running);\r\n",
            result.FormattedText);
    }

    [Fact]
    public void NamedInputAndOutputAlignmentCanBeConfiguredIndependently()
    {
        const string source =
            "fbRun(\n" +
            "a := 1,\n" +
            "longInput := 2,\n" +
            "x => first,\n" +
            "longOutput => second);";
        var options = FormatterOptions.Default with
        {
            Alignment = FormatterOptions.Default.Alignment with
            {
                NamedInputs = false,
                NamedOutputs = true
            }
        };

        var result = StructuredTextFormatter.Format(source, options);

        Assert.True(result.IsValid);
        Assert.Equal(
            "fbRun(\r\n" +
            "    a := 1,\r\n" +
            "    longInput := 2,\r\n" +
            "    x" + new string(' ', 10) + "=> first,\r\n" +
            "    longOutput => second);\r\n",
            result.FormattedText);
    }

    [Fact]
    public void DoesNotAlignNamedArgumentsAcrossNestedCallScopes()
    {
        const string source =
            "outer(\n" +
            "outerName := nested(\n" +
            "x := 1,\n" +
            "longInner := 2),\n" +
            "z := 3);";

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Equal(
            "outer(\r\n" +
            "    outerName := nested(\r\n" +
            "    x" + new string(' ', 9) + ":= 1,\r\n" +
            "    longInner := 2),\r\n" +
            "    z := 3);\r\n",
            result.FormattedText);
    }

    [Fact]
    public void NormalizesOpinionatedStructuralBlankLines()
    {
        const string source =
            "METHOD PRIVATE Configure\n\n\n" +
            "VAR_INPUT\nvalue : BOOL;\nEND_VAR\n\n" +
            "IF value THEN\nresult := TRUE;\nELSE\n\n\nresult := FALSE;\nEND_IF";

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Equal(
            "METHOD PRIVATE Configure\r\n" +
            "VAR_INPUT\r\n" +
            "    value : BOOL;\r\n" +
            "END_VAR\r\n" +
            "IF value THEN\r\n" +
            "    result := TRUE;\r\n" +
            "ELSE\r\n" +
            "    result := FALSE;\r\n" +
            "END_IF\r\n",
            result.FormattedText);
    }

    [Theory]
    [InlineData("VAR")]
    [InlineData("VAR_ACCESS")]
    [InlineData("VAR_CONFIG")]
    [InlineData("VAR_EXTERNAL")]
    [InlineData("VAR_GLOBAL")]
    [InlineData("VAR_IN_OUT")]
    [InlineData("VAR_INPUT")]
    [InlineData("VAR_INST")]
    [InlineData("VAR_OUTPUT")]
    [InlineData("VAR_STAT")]
    [InlineData("VAR_TEMP")]
    public void AddsBlankLineBeforeVariableBlocksAndKeepsTheirBoundariesTight(string keyword)
    {
        var result = StructuredTextFormatter.Format(
            $"previous := 1;\n{keyword}\n\nvalue : BOOL;\n\nEND_VAR",
            FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Equal(
            $"previous := 1;\r\n\r\n{keyword}\r\n    value : BOOL;\r\nEND_VAR\r\n",
            result.FormattedText);
    }

    [Fact]
    public void AppliesSptControlFlowBlankLineDefaults()
    {
        const string source =
            "ready := TRUE;\n" +
            "IF first THEN\n" +
            "value := 1;\n" +
            "ELSIF second THEN\n" +
            "CASE selector OF\n" +
            "0: value := 2;\n\n" +
            "END_CASE\n" +
            "ELSE\n\n" +
            "value := 3;\n\n" +
            "END_IF";

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Contains("ready := TRUE;\r\nIF first THEN", result.FormattedText, StringComparison.Ordinal);
        Assert.Contains("value := 1;\r\nELSIF second THEN", result.FormattedText, StringComparison.Ordinal);
        Assert.Contains("ELSIF second THEN\r\n    CASE selector OF", result.FormattedText, StringComparison.Ordinal);
        Assert.Contains("value := 2;\r\n    END_CASE\r\nELSE", result.FormattedText, StringComparison.Ordinal);
        Assert.Contains("value := 3;\r\nEND_IF", result.FormattedText, StringComparison.Ordinal);

        var secondPass = StructuredTextFormatter.Format(result.FormattedText, FormatterOptions.Default);
        Assert.True(secondPass.IsValid);
        Assert.Equal(result.FormattedText, secondPass.FormattedText);
    }

    [Fact]
    public void ConfiguresBlankLinesBeforeEveryStructuralKeyword()
    {
        const string source =
            "METHOD Configure\n" +
            "VAR_INPUT\nvalue : BOOL;\nEND_VAR\n" +
            "IF first THEN\n" +
            "value := 1;\n" +
            "ELSIF second THEN\n" +
            "CASE selector OF\n" +
            "0: value := 2;\n" +
            "END_CASE\n" +
            "ELSE\n" +
            "value := 3;\n" +
            "END_IF";
        var options = FormatterOptions.Default with
        {
            BlankLines = new BlankLineOptions(
                BeforeVariableBlock: BlankLinePolicy.Remove,
                BeforeIf: BlankLinePolicy.Remove,
                BeforeCase: BlankLinePolicy.Remove,
                BeforeIfElse: BlankLinePolicy.Remove,
                BeforeCaseElse: BlankLinePolicy.Remove,
                BeforeElsif: BlankLinePolicy.Remove,
                BeforeCaseLabel: BlankLinePolicy.Preserve,
                BeforeEndVar: BlankLinePolicy.Require,
                BeforeEndIf: BlankLinePolicy.Require,
                BeforeEndCase: BlankLinePolicy.Require,
                AfterIfThen: BlankLinePolicy.Preserve,
                AfterElsifThen: BlankLinePolicy.Preserve,
                AfterDo: BlankLinePolicy.Preserve,
                AfterCaseLabel: BlankLinePolicy.Preserve)
        };

        var result = StructuredTextFormatter.Format(source, options);

        Assert.True(result.IsValid);
        Assert.DoesNotContain("METHOD Configure\r\n\r\nVAR_INPUT", result.FormattedText, StringComparison.Ordinal);
        Assert.DoesNotContain("END_VAR\r\n\r\nIF", result.FormattedText, StringComparison.Ordinal);
        Assert.DoesNotContain("value := 1;\r\n\r\nELSIF", result.FormattedText, StringComparison.Ordinal);
        Assert.DoesNotContain("ELSIF second THEN\r\n\r\n    CASE", result.FormattedText, StringComparison.Ordinal);
        Assert.DoesNotContain("END_CASE\r\n\r\nELSE", result.FormattedText, StringComparison.Ordinal);
        Assert.Contains("value : BOOL;\r\n\r\nEND_VAR", result.FormattedText, StringComparison.Ordinal);
        Assert.Contains("value := 2;\r\n\r\n    END_CASE", result.FormattedText, StringComparison.Ordinal);
        Assert.Contains("value := 3;\r\n\r\nEND_IF", result.FormattedText, StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsIfBoundariesTightByDefault()
    {
        var result = StructuredTextFormatter.Format(
            "IF ready THEN\nvalue := 1;\nEND_IF",
            FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Equal(
            "IF ready THEN\r\n    value := 1;\r\nEND_IF\r\n",
            result.FormattedText);
    }

    [Fact]
    public void KeepsElsifBoundariesTightByDefault()
    {
        var result = StructuredTextFormatter.Format(
            "IF first THEN\nvalue := 1;\nELSIF second THEN\nvalue := 2;\nEND_IF",
            FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Contains(
            "value := 1;\r\nELSIF second THEN\r\n    value := 2;\r\nEND_IF",
            result.FormattedText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DistinguishesIfAndCaseElseDefaults()
    {
        const string source =
            "IF ready THEN\n" +
            "value := 1;\n" +
            "ELSE\n" +
            "value := 2;\n" +
            "END_IF\n" +
            "CASE mode OF\n" +
            "1: value := 3;\n" +
            "ELSE\n" +
            "value := 4;\n" +
            "END_CASE";

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Contains("value := 1;\r\nELSE", result.FormattedText, StringComparison.Ordinal);
        Assert.Contains("value := 3;\r\n\r\nELSE", result.FormattedText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("FOR index := 1 TO 3 DO", "END_FOR")]
    [InlineData("WHILE ready DO", "END_WHILE")]
    public void ConfiguresBlankLinesAfterDo(string header, string terminator)
    {
        var sourceWithBlankLine = $"{header}\n\nvalue := 1;\n{terminator}";
        var sourceWithoutBlankLine = $"{header}\nvalue := 1;\n{terminator}";

        var defaultResult = StructuredTextFormatter.Format(sourceWithBlankLine, FormatterOptions.Default);
        var requiredResult = StructuredTextFormatter.Format(
            sourceWithoutBlankLine,
            FormatterOptions.Default with
            {
                BlankLines = FormatterOptions.Default.BlankLines with
                {
                    AfterDo = BlankLinePolicy.Require
                }
            });
        var preservedWithBlankLine = StructuredTextFormatter.Format(
            sourceWithBlankLine,
            FormatterOptions.Default with
            {
                BlankLines = FormatterOptions.Default.BlankLines with
                {
                    AfterDo = BlankLinePolicy.Preserve
                }
            });
        var preservedWithoutBlankLine = StructuredTextFormatter.Format(
            sourceWithoutBlankLine,
            FormatterOptions.Default with
            {
                BlankLines = FormatterOptions.Default.BlankLines with
                {
                    AfterDo = BlankLinePolicy.Preserve
                }
            });

        Assert.True(defaultResult.IsValid);
        Assert.Contains($"{header}\r\n    value", defaultResult.FormattedText, StringComparison.Ordinal);
        Assert.True(requiredResult.IsValid);
        Assert.Contains($"{header}\r\n\r\n    value", requiredResult.FormattedText, StringComparison.Ordinal);
        Assert.True(preservedWithBlankLine.IsValid);
        Assert.Contains($"{header}\r\n\r\n    value", preservedWithBlankLine.FormattedText, StringComparison.Ordinal);
        Assert.True(preservedWithoutBlankLine.IsValid);
        Assert.Contains($"{header}\r\n    value", preservedWithoutBlankLine.FormattedText, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfiguresBlankLinesAfterCaseLabels()
    {
        const string sourceWithBlankLine = "CASE mode OF\n1:\n\nvalue := 1;\nEND_CASE";
        const string sourceWithoutBlankLine = "CASE mode OF\n1:\nvalue := 1;\nEND_CASE";

        var defaultResult = StructuredTextFormatter.Format(sourceWithBlankLine, FormatterOptions.Default);
        var requiredResult = StructuredTextFormatter.Format(
            sourceWithoutBlankLine,
            FormatterOptions.Default with
            {
                BlankLines = FormatterOptions.Default.BlankLines with
                {
                    AfterCaseLabel = BlankLinePolicy.Require
                }
            });
        var preservedWithBlankLine = StructuredTextFormatter.Format(
            sourceWithBlankLine,
            FormatterOptions.Default with
            {
                BlankLines = FormatterOptions.Default.BlankLines with
                {
                    AfterCaseLabel = BlankLinePolicy.Preserve
                }
            });
        var preservedWithoutBlankLine = StructuredTextFormatter.Format(
            sourceWithoutBlankLine,
            FormatterOptions.Default with
            {
                BlankLines = FormatterOptions.Default.BlankLines with
                {
                    AfterCaseLabel = BlankLinePolicy.Preserve
                }
            });

        Assert.True(defaultResult.IsValid);
        Assert.Contains("1:\r\n        value", defaultResult.FormattedText, StringComparison.Ordinal);
        Assert.True(requiredResult.IsValid);
        Assert.Contains("1:\r\n\r\n        value", requiredResult.FormattedText, StringComparison.Ordinal);
        Assert.True(preservedWithBlankLine.IsValid);
        Assert.Contains("1:\r\n\r\n        value", preservedWithBlankLine.FormattedText, StringComparison.Ordinal);
        Assert.True(preservedWithoutBlankLine.IsValid);
        Assert.Contains("1:\r\n        value", preservedWithoutBlankLine.FormattedText, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfiguresBlankLinesBeforeCaseLabels()
    {
        const string sourceWithBlankLines =
            "CASE mode OF\n\n1:\nvalue := 1;\n\n2:\nvalue := 2;\nEND_CASE";
        const string sourceWithoutBlankLines =
            "CASE mode OF\n1:\nvalue := 1;\n2:\nvalue := 2;\nEND_CASE";

        var defaultResult = StructuredTextFormatter.Format(sourceWithBlankLines, FormatterOptions.Default);
        var requiredResult = StructuredTextFormatter.Format(
            sourceWithoutBlankLines,
            FormatterOptions.Default with
            {
                BlankLines = FormatterOptions.Default.BlankLines with
                {
                    BeforeCaseLabel = BlankLinePolicy.Require
                }
            });
        var preservedWithBlankLines = StructuredTextFormatter.Format(
            sourceWithBlankLines,
            FormatterOptions.Default with
            {
                BlankLines = FormatterOptions.Default.BlankLines with
                {
                    BeforeCaseLabel = BlankLinePolicy.Preserve
                }
            });
        var preservedWithoutBlankLines = StructuredTextFormatter.Format(
            sourceWithoutBlankLines,
            FormatterOptions.Default with
            {
                BlankLines = FormatterOptions.Default.BlankLines with
                {
                    BeforeCaseLabel = BlankLinePolicy.Preserve
                }
            });

        Assert.True(defaultResult.IsValid);
        Assert.Contains("CASE mode OF\r\n    1:", defaultResult.FormattedText, StringComparison.Ordinal);
        Assert.Contains("value := 1;\r\n    2:", defaultResult.FormattedText, StringComparison.Ordinal);
        Assert.True(requiredResult.IsValid);
        Assert.Contains("CASE mode OF\r\n\r\n    1:", requiredResult.FormattedText, StringComparison.Ordinal);
        Assert.Contains("value := 1;\r\n\r\n    2:", requiredResult.FormattedText, StringComparison.Ordinal);
        Assert.True(preservedWithBlankLines.IsValid);
        Assert.Contains("CASE mode OF\r\n\r\n    1:", preservedWithBlankLines.FormattedText, StringComparison.Ordinal);
        Assert.Contains("value := 1;\r\n\r\n    2:", preservedWithBlankLines.FormattedText, StringComparison.Ordinal);
        Assert.True(preservedWithoutBlankLines.IsValid);
        Assert.Contains("CASE mode OF\r\n    1:", preservedWithoutBlankLines.FormattedText, StringComparison.Ordinal);
        Assert.Contains("value := 1;\r\n    2:", preservedWithoutBlankLines.FormattedText, StringComparison.Ordinal);
    }

    [Fact]
    public void PreservesConfiguredBlankLineBoundaries()
    {
        var options = FormatterOptions.Default with
        {
            BlankLines = FormatterOptions.Default.BlankLines with
            {
                BeforeIf = BlankLinePolicy.Preserve,
                AfterIfThen = BlankLinePolicy.Preserve
            }
        };

        var withBlankLines = StructuredTextFormatter.Format(
            "ready := TRUE;\n\nIF ready THEN\n\nvalue := 1;\nEND_IF",
            options);
        var withoutBlankLines = StructuredTextFormatter.Format(
            "ready := TRUE;\nIF ready THEN\nvalue := 1;\nEND_IF",
            options);

        Assert.True(withBlankLines.IsValid);
        Assert.Contains("ready := TRUE;\r\n\r\nIF ready THEN\r\n\r\n", withBlankLines.FormattedText);
        Assert.True(withoutBlankLines.IsValid);
        Assert.Contains("ready := TRUE;\r\nIF ready THEN\r\n    value := 1;", withoutBlankLines.FormattedText);
    }

    [Fact]
    public void KeepsBlankLinesAfterOtherStatements()
    {
        const string source = "first := 1;\n\nsecond := 2;";

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Equal("first := 1;\r\n\r\nsecond := 2;\r\n", result.FormattedText);
    }

    [Fact]
    public void SplitsAdjacentTopLevelStatementsAndKeepsTrailingBlockCommentAttached()
    {
        var options = FormatterOptions.Default with
        {
            Alignment = FormatterOptions.Default.Alignment with { Assignments = false }
        };

        var result = StructuredTextFormatter.Format("first:=1; (* keep *) second:=2;", options);

        Assert.True(result.IsValid);
        Assert.Equal("first := 1; (* keep *)\r\nsecond := 2;\r\n", result.FormattedText);
    }

    [Fact]
    public void CanPreserveAdjacentTopLevelStatements()
    {
        var options = FormatterOptions.Default with
        {
            Layout = FormatterOptions.Default.Layout with { OneStatementPerLine = false },
            Alignment = FormatterOptions.Default.Alignment with { Assignments = false }
        };

        var result = StructuredTextFormatter.Format("first:=1; second:=2;", options);

        Assert.True(result.IsValid);
        Assert.Equal("first := 1; second := 2;\r\n", result.FormattedText);
    }

    [Fact]
    public void AlwaysWrapsCallArgumentsAndClosingDelimiter()
    {
        var options = FormatterOptions.Default with
        {
            Wrapping = FormatterOptions.Default.Wrapping with { Calls = WrapStyle.Always },
            Alignment = FormatterOptions.Default.Alignment with { NamedInputs = false }
        };

        var result = StructuredTextFormatter.Format("fbRun(first:=1, second:=2);", options);

        Assert.True(result.IsValid);
        Assert.Equal(
            "fbRun(\r\n" +
            "    first := 1,\r\n" +
            "    second := 2\r\n" +
            ");\r\n",
            result.FormattedText);
    }

    [Fact]
    public void HangingWrapsMultilineCallsAndAlignsNamedArguments()
    {
        var options = FormatterOptions.Default with
        {
            Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 0 },
            Wrapping = FormatterOptions.Default.Wrapping with { Calls = WrapStyle.Hanging }
        };
        const string prefix = "result := builder.Build(";
        var indentation = new string(' ', prefix.Length);

        var result = StructuredTextFormatter.Format(
            "result := builder.Build(a := 1,\nlongName := 2, z => output);",
            options);

        Assert.True(result.IsValid);
        Assert.Equal(
            "result := builder.Build(a" + new string(' ', 8) + ":= 1,\r\n" +
            indentation + "longName := 2,\r\n" +
            indentation + "z" + new string(' ', 8) + "=> output);\r\n",
            result.FormattedText);

        var secondPass = StructuredTextFormatter.Format(result.FormattedText, options);
        Assert.True(secondPass.IsValid);
        Assert.Equal(result.FormattedText, secondPass.FormattedText);
    }

    [Fact]
    public void HangingKeepsShortMultiArgumentCallsOnOneLine()
    {
        var options = FormatterOptions.Default with
        {
            Wrapping = FormatterOptions.Default.Wrapping with { Calls = WrapStyle.Hanging },
            Alignment = FormatterOptions.Default.Alignment with { NamedInputs = false }
        };

        var result = StructuredTextFormatter.Format("fbRun(first := 1, second := 2);", options);

        Assert.True(result.IsValid);
        Assert.Equal("fbRun(first := 1, second := 2);\r\n", result.FormattedText);
    }

    [Fact]
    public void HangingWrapsEveryArgumentAfterTheFirstWhenCallIsLong()
    {
        var options = FormatterOptions.Default with
        {
            Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 30 },
            Wrapping = FormatterOptions.Default.Wrapping with { Calls = WrapStyle.Hanging },
            Alignment = FormatterOptions.Default.Alignment with { NamedInputs = false }
        };
        const string prefix = "result := Build(";
        var indentation = new string(' ', prefix.Length);

        var result = StructuredTextFormatter.Format(
            "result := Build(firstArgument := 1, secondArgument := 2, thirdArgument := 3);",
            options);

        Assert.True(result.IsValid);
        Assert.Equal(
            "result := Build(firstArgument := 1,\r\n" +
            indentation + "secondArgument := 2,\r\n" +
            indentation + "thirdArgument := 3);\r\n",
            result.FormattedText);
    }

    [Fact]
    public void WrapsLongArrayInitializerAtAvailableCommas()
    {
        var options = FormatterOptions.Default with
        {
            Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 32 },
            Alignment = FormatterOptions.Default.Alignment with { Assignments = false }
        };

        var result = StructuredTextFormatter.Format(
            "values := [firstValue, secondValue, thirdValue];",
            options);

        Assert.True(result.IsValid);
        Assert.Equal(
            "values := [\r\n" +
            "    firstValue, secondValue,\r\n" +
            "    thirdValue];\r\n",
            result.FormattedText);
    }

    [Fact]
    public void MovesFirstFieldOfMultilineStructureInitializerToContinuationLine()
    {
        const string source =
            "VAR\n" +
            "_safetyGroup : FB_Component_Safety_Group := (Name := 'TwinSafeGroup1',\n" +
            "SafetyReset := _safetyReset,\n" +
            "AutoResetConnectionFaults := TRUE);\n" +
            "END_VAR";

        var result = StructuredTextFormatter.Format(source, FormatterOptions.Default);

        Assert.True(result.IsValid);
        Assert.Equal(
            "VAR\r\n" +
            "    _safetyGroup : FB_Component_Safety_Group := (\r\n" +
            "        Name" + new string(' ', 22) + ":= 'TwinSafeGroup1',\r\n" +
            "        SafetyReset" + new string(' ', 15) + ":= _safetyReset,\r\n" +
            "        AutoResetConnectionFaults := TRUE);\r\n" +
            "END_VAR\r\n",
            result.FormattedText);
    }

    [Fact]
    public void KeepsShortMultiParameterCallOnOneLine()
    {
        var options = FormatterOptions.Default with
        {
            Alignment = FormatterOptions.Default.Alignment with { NamedInputs = false }
        };

        var result = StructuredTextFormatter.Format("fbRun(first := 1, second := 2);", options);

        Assert.True(result.IsValid);
        Assert.Equal("fbRun(first := 1, second := 2);\r\n", result.FormattedText);
    }

    [Theory]
    [InlineData(BinaryOperatorPosition.After,
        "result := firstCondition AND\r\n    secondCondition OR\r\n    thirdCondition;\r\n")]
    [InlineData(BinaryOperatorPosition.Before,
        "result := firstCondition\r\n    AND secondCondition\r\n    OR thirdCondition;\r\n")]
    public void WrapsLongBinaryExpressionsAtConfiguredOperatorPosition(
        BinaryOperatorPosition position,
        string expected)
    {
        var options = FormatterOptions.Default with
        {
            Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 36 },
            Wrapping = FormatterOptions.Default.Wrapping with { BinaryOperatorPosition = position },
            Alignment = FormatterOptions.Default.Alignment with { Assignments = false }
        };

        var result = StructuredTextFormatter.Format(
            "result := firstCondition AND secondCondition OR thirdCondition;",
            options);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.FormattedText);
    }

    [Fact]
    public void WrappingIsIdempotentAndPreservesCommentsAndStrings()
    {
        var options = FormatterOptions.Default with
        {
            Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 30 },
            Alignment = FormatterOptions.Default.Alignment with { NamedInputs = false }
        };
        const string source = "fbLog(message := 'a,b AND c', (* note *) enabled := first AND second);";

        var first = StructuredTextFormatter.Format(source, options);
        var second = StructuredTextFormatter.Format(first.FormattedText, options);

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.Contains("'a,b AND c'", first.FormattedText, StringComparison.Ordinal);
        Assert.Contains("(* note *)", first.FormattedText, StringComparison.Ordinal);
        Assert.Equal(first.FormattedText, second.FormattedText);
    }

    [Fact]
    public void PreserveLeavesLongCallsOnTheirExistingLines()
    {
        var options = FormatterOptions.Default with
        {
            Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 20 },
            Wrapping = FormatterOptions.Default.Wrapping with
            {
                Calls = WrapStyle.Preserve,
                BinaryExpressions = WrapStyle.Preserve
            },
            Alignment = FormatterOptions.Default.Alignment with { NamedInputs = false }
        };

        var result = StructuredTextFormatter.Format("fbRun(first:=1, second:=2);", options);

        Assert.True(result.IsValid);
        Assert.Equal("fbRun(first := 1, second := 2);\r\n", result.FormattedText);
    }

    [Fact]
    public void AlwaysWrappingStillAppliesWhenMaximumLineLengthIsOff()
    {
        var options = FormatterOptions.Default with
        {
            Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 0 },
            Wrapping = FormatterOptions.Default.Wrapping with { Initializers = WrapStyle.Always },
            Alignment = FormatterOptions.Default.Alignment with { Assignments = false }
        };

        var result = StructuredTextFormatter.Format("values := [one, two];", options);

        Assert.True(result.IsValid);
        Assert.Equal(
            "values := [\r\n" +
            "    one,\r\n" +
            "    two\r\n" +
            "];\r\n",
            result.FormattedText);
    }

    [Fact]
    public void NestedAlwaysWrappedCallsRemainIdempotent()
    {
        var options = FormatterOptions.Default with
        {
            Wrapping = FormatterOptions.Default.Wrapping with { Calls = WrapStyle.Always },
            Alignment = FormatterOptions.Default.Alignment with
            {
                NamedInputs = false,
                NamedOutputs = false
            }
        };
        const string source = "outer(inner(a := 1, b := 2), done => complete);";

        var first = StructuredTextFormatter.Format(source, options);
        var second = StructuredTextFormatter.Format(first.FormattedText, options);

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.Equal(first.FormattedText, second.FormattedText);
        Assert.Contains("outer(\r\n", first.FormattedText, StringComparison.Ordinal);
        Assert.Contains("inner(\r\n", first.FormattedText, StringComparison.Ordinal);
    }

    [Fact]
    public void BinaryPlusKeepsBinarySpacingWhenPlacedBeforeBreak()
    {
        var options = FormatterOptions.Default with
        {
            Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 24 },
            Wrapping = FormatterOptions.Default.Wrapping with
            {
                BinaryOperatorPosition = BinaryOperatorPosition.Before
            },
            Alignment = FormatterOptions.Default.Alignment with { Assignments = false }
        };

        var result = StructuredTextFormatter.Format("result := firstValue + secondValue;", options);

        Assert.True(result.IsValid);
        Assert.Equal("result := firstValue\r\n    + secondValue;\r\n", result.FormattedText);
    }

    [Fact]
    public void AlwaysWrappedCallsUseConfiguredTabContinuation()
    {
        var options = FormatterOptions.Default with
        {
            Indentation = FormatterOptions.Default.Indentation with { Style = IndentStyle.Tabs },
            Wrapping = FormatterOptions.Default.Wrapping with { Calls = WrapStyle.Always },
            Alignment = FormatterOptions.Default.Alignment with { NamedInputs = false }
        };

        var result = StructuredTextFormatter.Format("fbRun(first:=1, second:=2);", options);

        Assert.True(result.IsValid);
        Assert.Equal(
            "fbRun(\r\n" +
            "\tfirst := 1,\r\n" +
            "\tsecond := 2\r\n" +
            ");\r\n",
            result.FormattedText);
    }

    [Fact]
    public void InitializerWrappingDoesNotTreatGroupedExpressionAsStructureInitializer()
    {
        var options = FormatterOptions.Default with
        {
            Wrapping = FormatterOptions.Default.Wrapping with
            {
                Initializers = WrapStyle.Always,
                BinaryExpressions = WrapStyle.Preserve
            },
            Alignment = FormatterOptions.Default.Alignment with { Assignments = false }
        };

        var result = StructuredTextFormatter.Format("result := (first + second);", options);

        Assert.True(result.IsValid);
        Assert.Equal("result := (first + second);\r\n", result.FormattedText);
    }

    [Fact]
    public void AlwaysWrapsStructureInitializerFields()
    {
        var options = FormatterOptions.Default with
        {
            Wrapping = FormatterOptions.Default.Wrapping with { Initializers = WrapStyle.Always },
            Alignment = FormatterOptions.Default.Alignment with
            {
                Assignments = false,
                NamedInputs = false
            }
        };

        var result = StructuredTextFormatter.Format("value := (first := 1, second := 2);", options);

        Assert.True(result.IsValid);
        Assert.Equal(
            "value := (\r\n" +
            "    first := 1,\r\n" +
            "    second := 2\r\n" +
            ");\r\n",
            result.FormattedText);
    }
}

