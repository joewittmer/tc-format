using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class StatementLayoutTests
{
    [Fact]
    public void SeparatesCompleteArrayDeclarationsAndKeepsEntriesTogether()
    {
        AssertFormatted("VAR\ncount : INT;\nnames : ARRAY[0..1] OF STRING(80) := [\n'Wedge Axis 1',\n'Wedge Axis 2'];\nready : BOOL;\nEND_VAR",
            "VAR\r\n    count : INT;\r\n\r\n    names : ARRAY[0..1] OF STRING(80) := [\r\n        'Wedge Axis 1',\r\n        'Wedge Axis 2'];\r\n\r\n    ready : BOOL;\r\nEND_VAR\r\n");
    }

    [Fact]
    public void AppliesOneSharedBoundaryRuleToCallsAssignmentsAndDeclarations()
    {
        AssertFormatted("a := 1;\nb := 2;\nx := first OR\nsecond;\nRun(1,\n2);\nDone();",
            "a := 1;\r\nb := 2;\r\n\r\nx := first OR\r\n     second;\r\n\r\nRun(1,\r\n    2);\r\n\r\nDone();\r\n");
    }

    [Fact]
    public void SeparatesStructureDeclarationsAfterWrapping()
    {
        var options = Options with { Wrapping = Options.Wrapping with { Initializers = WrapStyle.Always } };
        AssertFormatted("VAR\ncount : INT;\nitem : ST_Item := (Name := 'one', Value := 1);\nready : BOOL;\nEND_VAR",
            "VAR\r\n    count : INT;\r\n\r\n    item : ST_Item := (\r\n        Name := 'one',\r\n        Value := 1);\r\n\r\n    ready : BOOL;\r\nEND_VAR\r\n", options);
    }

    [Fact]
    public void AttachesLeadingCommentsAndAvoidsBlankLinesInsideExpressions()
    {
        var options = Options with { BlankLines = Options.BlankLines with { BeforeComment = BlankLinePolicy.Remove, AfterComment = BlankLinePolicy.Remove } };
        AssertFormatted("a := 1;\n// names for display\nnames : ARRAY[0..1] OF STRING := [\n'one',\n\n// second name\n'two'];\n// count\ncount := 2;",
            "a := 1;\r\n\r\n// names for display\r\nnames : ARRAY[0..1] OF STRING := [\r\n    'one',\r\n    // second name\r\n    'two'];\r\n\r\n// count\r\ncount := 2;\r\n", options);
    }

    [Theory]
    [InlineData(BlankLinePolicy.Require, "\r\n\r\n")]
    [InlineData(BlankLinePolicy.Remove, "\r\n")]
    public void GeneralPolicyOverridesNarrowerStatementRules(BlankLinePolicy policy, string gap)
    {
        var options = Options with
        {
            BlankLines = Options.BlankLines with
            {
                AroundMultilineStatements = policy,
                AfterMultilineCall = policy == BlankLinePolicy.Require ? BlankLinePolicy.Remove : BlankLinePolicy.Require,
                BeforeMultilineAssignment = BlankLinePolicy.Remove,
                AfterMultilineAssignment = BlankLinePolicy.Remove
            }
        };
        AssertFormatted("Run(1,\n2);\n\nx := 1;", "Run(1,\r\n    2);" + gap + "x := 1;\r\n", options);
    }

    [Fact]
    public void BlockBoundaryPoliciesStillTakePrecedence()
    {
        AssertFormatted("IF ready THEN\nx := first OR\nsecond;\nELSE\nRun(1,\n2);\nEND_IF",
            "IF ready THEN\r\n    x := first OR\r\n         second;\r\nELSE\r\n    Run(1,\r\n        2);\r\nEND_IF\r\n");
        AssertFormatted("CASE state OF\n1:\nx := first OR\nsecond;\nEND_CASE",
            "CASE state OF\r\n    1:\r\n        x := first OR\r\n             second;\r\nEND_CASE\r\n");
    }

    [Fact]
    public void GlobalLimitAndFinalFormattedLineCountApply()
    {
        AssertFormatted("Prepare();\nx := Call(1\n);\nDone();", "Prepare();\r\nx := Call(1);\r\nDone();\r\n");
        AssertFormatted("Prepare();\nRun(1,\n2);\nDone();", "Prepare();\r\nRun(1,\r\n    2);\r\nDone();\r\n",
            Options with { Layout = Options.Layout with { MaximumConsecutiveBlankLines = 0 } });
    }

    [Fact]
    public void MultilineBodiesDoNotMakeSingleLineHeadersMultiline()
    {
        AssertFormatted("Prepare();\nIF ready THEN\nRun();\nDone();\nEND_IF\nFinish();",
            "Prepare();\r\nIF ready THEN\r\n    Run();\r\n    Done();\r\nEND_IF\r\nFinish();\r\n");
    }

    private static FormatterOptions Options => FormatterOptions.Default with
    {
        Layout = FormatterOptions.Default.Layout with { MaximumLineLength = 0 },
        Alignment = new AlignmentOptions(false, false, false, false, false, false, false),
        Wrapping = FormatterOptions.Default.Wrapping with
        {
            Calls = WrapStyle.Preserve,
            Initializers = WrapStyle.Preserve,
            BinaryExpressions = WrapStyle.Preserve,
            MultilineClosingParenthesis = ClosingDelimiterStyle.SameLine,
            MultilineClosingBracket = ClosingDelimiterStyle.SameLine
        },
        BlankLines = FormatterOptions.Default.BlankLines with { AroundMultilineStatements = BlankLinePolicy.Require }
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
