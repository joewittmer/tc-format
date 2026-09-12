using TcFormat.Xae;

using Xunit;

namespace TcFormat.Xae.Tests;

public sealed class SectionTextTests
{
    [Theory]
    [InlineData("VAR\nx:BOOL;\nEND_VAR\n\n", "VAR\r\nx : BOOL;\r\nEND_VAR\r\n\r\n", "VAR\nx : BOOL;\nEND_VAR")]
    [InlineData("Run();\r\n\r\n", "Run();\n\n", "Run();")]
    [InlineData("// heading\nRun();", "// heading\r\nRun();\r\n", "// heading\nRun();")]
    [InlineData("", "\r\n", "")]
    public void RemovesFinalEmptyRowsAndKeepsPaneLineEndings(string source, string formatted, string expected)
    {
        Assert.Equal(expected, FormatterProcess.NormalizeSectionText(source, formatted));
    }
}
