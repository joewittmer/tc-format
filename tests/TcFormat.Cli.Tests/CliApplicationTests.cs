using TcFormat.Cli;

using Xunit;

namespace TcFormat.Cli.Tests;

public sealed class CliApplicationTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"tc-format-cli-tests-{Guid.NewGuid():N}");

    public CliApplicationTests() => Directory.CreateDirectory(temporaryDirectory);

    [Fact]
    public void VersionContainsExactlyFourNumericFields()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliApplication.Run(
            ["--version"],
            new StringReader(string.Empty),
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Matches(@"^tc_format \d+\.\d+\.\d+\.\d+\r?\n$", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void StdinFilepathFormatsRawStructuredTextWithoutWritingBackingFile()
    {
        var filePath = Path.Combine(temporaryDirectory, "Example.TcPOU");
        const string backingContents = "backing file must not change";
        File.WriteAllText(filePath, backingContents);
        File.WriteAllText(
            Path.Combine(temporaryDirectory, ".editorconfig"),
            """
            root = true

            [*.TcPOU]
            tc_format_keyword_case = upper
            insert_final_newline = false
            """);
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliApplication.Run(
            ["--stdin-filepath", filePath],
            new StringReader("if ready then result:=1;end_if;"),
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal("IF ready THEN result := 1;\r\nEND_IF;", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
        Assert.Equal(backingContents, File.ReadAllText(filePath));
    }

    [Fact]
    public void StdinFilepathRejectsInvalidConfigurationWithoutWritingStandardOutput()
    {
        var filePath = Path.Combine(temporaryDirectory, "Example.TcPOU");
        File.WriteAllText(
            Path.Combine(temporaryDirectory, ".editorconfig"),
            """
            root = true

            [*.TcPOU]
            tc_format_blank_line_after_do = sometimes
            """);
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliApplication.Run(
            ["--stdin-filepath", filePath],
            new StringReader("result := 1;"),
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Invalid value 'sometimes'", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void StdinFilepathRequiresExactlyOnePath()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliApplication.Run(
            ["--stdin-filepath"],
            new StringReader(string.Empty),
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("requires exactly one file path", error.ToString(), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(temporaryDirectory, recursive: true);
    }
}
