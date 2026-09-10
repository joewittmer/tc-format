using TcFormat.Core;

using Xunit;

namespace TcFormat.Formatting.Tests;

public sealed class KeywordParenthesisSpacingTests
{
    [Theory]
    [InlineData(true, " ")]
    [InlineData(false, "")]
    public void SharesSettingAcrossIfAndLogicalOperators(bool enabled, string gap)
    {
        var options = FormatterOptions.Default with
        {
            Spacing = FormatterOptions.Default.Spacing with { BeforeParenthesesAfterKeywords = enabled }
        };

        AssertFormatted("IF(first)AND (second)OR(third) THEN\nRun();\nEND_IF",
            $"IF{gap}(first) AND{gap}(second) OR{gap}(third) THEN\r\n    Run();\r\nEND_IF\r\n", options);
    }

    [Theory]
    [InlineData("AND")]
    [InlineData("AND_THEN")]
    [InlineData("OR")]
    [InlineData("OR_ELSE")]
    [InlineData("XOR")]
    [InlineData("MOD")]
    public void SpacesWordOperatorsBeforeParentheses(string keyword)
    {
        AssertFormatted($"value := first {keyword}(second);",
            $"value := first {keyword} (second);\r\n", FormatterOptions.Default);
    }

    [Theory]
    [InlineData(KeywordCase.Upper, "IF", "AND", "OR", "NOT", "THEN", "END_IF")]
    [InlineData(KeywordCase.Lower, "if", "and", "or", "not", "then", "end_if")]
    [InlineData(KeywordCase.Preserve, "iF", "aNd", "oR", "nOt", "tHeN", "eNd_If")]
    public void SupportsKeywordCaseAndIndependentInsideSpacing(
        KeywordCase keywordCase, string ifKeyword, string andKeyword, string orKeyword,
        string notKeyword, string thenKeyword, string endKeyword)
    {
        var options = FormatterOptions.Default with
        {
            KeywordCase = keywordCase,
            Spacing = FormatterOptions.Default.Spacing with { InsideParentheses = true, AroundBinaryOperators = false }
        };
        AssertFormatted("iF(first)aNd(second)oR nOt(third)tHeN\nRun();\neNd_If",
            $"{ifKeyword} ( first ) {andKeyword} ( second ) {orKeyword} {notKeyword} ( third ) {thenKeyword}\r\n    Run();\r\n{endKeyword}\r\n", options);
    }

    [Theory]
    [InlineData("IF(first) THEN\nELSIF(second) THEN\nEND_IF", "IF (first) THEN\r\nELSIF (second) THEN\r\nEND_IF\r\n")]
    [InlineData("WHILE(first) DO\nEND_WHILE", "WHILE (first) DO\r\nEND_WHILE\r\n")]
    [InlineData("REPEAT\nUNTIL(first)\nEND_REPEAT", "REPEAT\r\nUNTIL (first)\r\nEND_REPEAT\r\n")]
    [InlineData("CASE(value) OF\nEND_CASE", "CASE (value) OF\r\nEND_CASE\r\n")]
    public void SpacesRelatedControlKeywords(string source, string expected)
    {
        AssertFormatted(source, expected, FormatterOptions.Default);
    }

    [Fact]
    public void PreservesCallsIndexingCommentsAndLiterals()
    {
        AssertFormatted("IF(Check(values[1]) AND(SIZEOF(value) > 0)) THEN // IF( AND(\n" +
                        "Run('OR(');\nEND_IF",
            "IF (Check(values[1]) AND (SIZEOF(value) > 0)) THEN // IF( AND(\r\n" +
            "    Run('OR(');\r\nEND_IF\r\n", FormatterOptions.Default);
    }

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
