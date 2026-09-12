using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class CommentBlankLineTests
{
    [Theory]
    [InlineData("// heading")]
    [InlineData("(* heading *)")]
    [InlineData("(* heading\n\n   (* nested *)\n   details *)")]
    public void RemovesBlankLinesAfterEitherCommentStyle(string comment)
    {
        var options = FormatterOptions.Default with
        {
            BlankLines = FormatterOptions.Default.BlankLines with { AfterComment = BlankLinePolicy.Remove }
        };
        AssertFormatted(comment + "\n\n\nRun();", comment + "\r\nRun();\r\n", options);
    }

    [Theory]
    [InlineData(BlankLinePolicy.Require, "\n", "\r\n\r\n")]
    [InlineData(BlankLinePolicy.Require, "\n\n\n", "\r\n\r\n")]
    [InlineData(BlankLinePolicy.Preserve, "\n", "\r\n")]
    [InlineData(BlankLinePolicy.Preserve, "\n\n", "\r\n\r\n")]
    public void ControlsAfterCommentSpacing(BlankLinePolicy policy, string sourceGap, string expectedGap)
    {
        var options = FormatterOptions.Default with
        {
            BlankLines = FormatterOptions.Default.BlankLines with { AfterComment = policy }
        };
        AssertFormatted("// heading\n(* detail *)" + sourceGap + "Run();",
            "// heading\r\n(* detail *)" + expectedGap + "Run();\r\n", options);
    }

    [Fact]
    public void KeepsInternalCommentGapsAndIgnoresTrailingComments()
    {
        var options = FormatterOptions.Default with
        {
            BlankLines = FormatterOptions.Default.BlankLines with { AfterComment = BlankLinePolicy.Remove }
        };
        AssertFormatted("// heading\n\n(* details *)\n\nRun(); // trailing\n\nFinish();",
            "// heading\r\n\r\n(* details *)\r\nRun(); // trailing\r\n\r\nFinish();\r\n", options);
    }

    [Fact]
    public void AfterCommentRemovalTakesPrecedenceOverBeforeKeywordSpacing()
    {
        var options = FormatterOptions.Default with
        {
            BlankLines = FormatterOptions.Default.BlankLines with
            {
                BeforeComment = BlankLinePolicy.Require,
                AfterComment = BlankLinePolicy.Remove,
                BeforeIf = BlankLinePolicy.Require
            }
        };
        AssertFormatted("Prepare();\n// heading\n\nIF ready THEN\nRun();\nEND_IF",
            "Prepare();\r\n\r\n// heading\r\nIF ready THEN\r\n    Run();\r\nEND_IF\r\n", options);
    }

    [Theory]
    [InlineData("less-whitespace")]
    [InlineData("more-whitespace")]
    [InlineData("more-whitespace-without-assignment-alignment")]
    public void ProfilesRemoveBlankLinesAfterComments(string profile)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Profiles", profile, "Example.st");
        var configuration = new EditorConfigResolver().Resolve(path);
        Assert.True(configuration.IsValid);
        AssertFormatted("// heading\n\nRun();", "// heading\r\nRun();\r\n", configuration.Options);
    }

    [Theory]
    [InlineData("// heading")]
    [InlineData("(* heading *)")]
    [InlineData("(* heading\n   (* nested comment *)\n   details *)")]
    public void RequiresOneBlankLineBeforeEitherCommentStyle(string comment)
    {
        var expected = "Prepare();\r\n\r\n" + comment + "\r\nRun();\r\n";
        AssertFormatted("Prepare();\n" + comment + "\nRun();", expected, Options(BlankLinePolicy.Require));
    }

    [Theory]
    [InlineData(BlankLinePolicy.Remove, "\n\n", "\r\n")]
    [InlineData(BlankLinePolicy.Preserve, "\n", "\r\n")]
    [InlineData(BlankLinePolicy.Preserve, "\n\n", "\r\n\r\n")]
    [InlineData(BlankLinePolicy.Require, "\n\n\n", "\r\n\r\n")]
    public void SupportsRemovePreserveAndRequire(BlankLinePolicy policy, string sourceGap, string expectedGap)
    {
        AssertFormatted("Prepare();" + sourceGap + "(* heading *)\n// details\nRun();",
            "Prepare();" + expectedGap + "(* heading *)\r\n// details\r\nRun();\r\n", Options(policy));
    }

    [Fact]
    public void KeepsMixedConsecutiveCommentsTogetherAndDoesNotAddLeadingBlankLine()
    {
        AssertFormatted("// file heading\n(* details *)\nPrepare();\n// section\n(* more details *)\n// final detail\nRun();",
            "// file heading\r\n(* details *)\r\nPrepare();\r\n\r\n// section\r\n(* more details *)\r\n// final detail\r\nRun();\r\n",
            Options(BlankLinePolicy.Require));
    }

    [Fact]
    public void LeavesTrailingAndInlineCommentsAttached()
    {
        AssertFormatted("Prepare();// trailing\nRun(); (* trailing *)\n(* inline *) Finish();",
            "Prepare(); // trailing\r\nRun(); (* trailing *)\r\n(* inline *) Finish();\r\n", Options(BlankLinePolicy.Require));
    }

    [Fact]
    public void WorksInsideNestedBlocks()
    {
        AssertFormatted("IF ready THEN\nPrepare();\n// section\nRun();\nEND_IF",
            "IF ready THEN\r\n    Prepare();\r\n\r\n    // section\r\n    Run();\r\nEND_IF\r\n", Options(BlankLinePolicy.Require));
    }

    [Theory]
    [InlineData("less-whitespace", "\r\n")]
    [InlineData("more-whitespace", "\r\n\r\n")]
    [InlineData("more-whitespace-without-assignment-alignment", "\r\n\r\n")]
    public void ProfilesChooseCommentGroupSpacing(string profile, string gap)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Profiles", profile, "Example.st");
        var configuration = new EditorConfigResolver().Resolve(path);
        Assert.True(configuration.IsValid);
        AssertFormatted("Prepare();\n\n// heading\n(* details *)\nRun();",
            "Prepare();" + gap + "// heading\r\n(* details *)\r\nRun();\r\n", configuration.Options);
    }

    [Fact]
    public void HonorsExistingBlockBoundaryRulesAndGlobalBlankLineLimit()
    {
        AssertFormatted("IF ready THEN\n\n// heading\nRun();\nELSE\n\n// alternative\nWait();\nEND_IF",
            "IF ready THEN\r\n    // heading\r\n    Run();\r\nELSE\r\n    // alternative\r\n    Wait();\r\nEND_IF\r\n",
            Options(BlankLinePolicy.Require));
        AssertFormatted("Prepare();\n// heading\nRun();", "Prepare();\r\n// heading\r\nRun();\r\n",
            Options(BlankLinePolicy.Require) with { Layout = FormatterOptions.Default.Layout with { MaximumConsecutiveBlankLines = 0 } });
    }

    private static FormatterOptions Options(BlankLinePolicy policy) => FormatterOptions.Default with
    {
        BlankLines = FormatterOptions.Default.BlankLines with { BeforeComment = policy }
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
