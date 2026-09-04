using System.Text;

using TcFormat.TwinCat;

using Xunit;

namespace TcFormat.TwinCat.Tests;

public sealed class SourceDocumentTests
{
    [Fact]
    public void PlainTextFileUsesEntireDocumentAsCodeRegion()
    {
        using var file = TemporaryFile.Create("Program.st", "PROGRAM Main\r\nEND_PROGRAM\r\n");

        var document = SourceDocumentLoader.Load(file.Path);
        var region = Assert.Single(document.Regions);

        Assert.Equal(CodeRegionKind.PlainText, region.Kind);
        Assert.Equal(document.Text, document.GetRegionText(region));
        Assert.Equal(file.Bytes, document.Encode());
    }

    [Fact]
    public void Utf8BomIsPreservedExactly()
    {
        var body = Encoding.UTF8.GetBytes("PROGRAM Main\r\nEND_PROGRAM\r\n");
        var bytes = Encoding.UTF8.Preamble.ToArray().Concat(body).ToArray();
        using var file = TemporaryFile.Create("Program.st", bytes);

        var document = SourceDocumentLoader.Load(file.Path);

        Assert.Equal(SourceEncodingKind.Utf8Bom, document.Encoding.Kind);
        Assert.Equal(bytes, document.Encode());
    }

    [Fact]
    public void TwinCatXmlExtractsDeclarationAndImplementationWithoutReserializingContainer()
    {
        const string xml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <TcPlcObject Version="1.1.0.1" ProductVersion="3.1.4026.0">
              <POU Name="Main" Id="{11111111-1111-1111-1111-111111111111}">
                <Declaration><![CDATA[PROGRAM Main
            VAR
                value:INT;
            END_VAR]]></Declaration>
                <Implementation><ST><![CDATA[value:=1;]]></ST></Implementation>
              </POU>
            </TcPlcObject>
            """;
        using var file = TemporaryFile.Create("Main.TcPOU", xml);

        var document = SourceDocumentLoader.Load(file.Path);

        Assert.Collection(
            document.Regions,
            region =>
            {
                Assert.Equal(CodeRegionKind.Declaration, region.Kind);
                Assert.StartsWith("PROGRAM Main", document.GetRegionText(region), StringComparison.Ordinal);
            },
            region =>
            {
                Assert.Equal(CodeRegionKind.Implementation, region.Kind);
                Assert.Equal("value:=1;", document.GetRegionText(region));
            });

        var declaration = document.GetRegionText(document.Regions[0]);
        var implementation = document.GetRegionText(document.Regions[1]);
        var replaced = document.ReplaceRegions(["DECLARATION", "IMPLEMENTATION"]);
        Assert.Equal(
            xml.Replace(declaration, "DECLARATION", StringComparison.Ordinal)
                .Replace(implementation, "IMPLEMENTATION", StringComparison.Ordinal),
            replaced.Text);
    }

    [Fact]
    public void UnrelatedCDataIsIgnored()
    {
        const string xml =
            """
            <Root>
              <Metadata><![CDATA[not structured text]]></Metadata>
              <Declaration><![CDATA[VAR_GLOBAL
            END_VAR]]></Declaration>
            </Root>
            """;
        using var file = TemporaryFile.Create("Globals.TcGVL", xml);

        var document = SourceDocumentLoader.Load(file.Path);

        var region = Assert.Single(document.Regions);
        Assert.Equal($"VAR_GLOBAL{Environment.NewLine}END_VAR", document.GetRegionText(region));
    }

    [Fact]
    public void BomlessUnsupportedXmlEncodingIsRejected()
    {
        const string xml = "<?xml version=\"1.0\" encoding=\"windows-1252\"?><Declaration><![CDATA[]]></Declaration>";
        using var file = TemporaryFile.Create("Globals.TcGVL", Encoding.ASCII.GetBytes(xml));

        var exception = Assert.Throws<SourceDocumentException>(() => SourceDocumentLoader.Load(file.Path));

        Assert.Contains("unsupported XML encoding", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MalformedTwinCatXmlIsRejectedBeforeExtraction()
    {
        const string xml = "<Root><Declaration><![CDATA[VAR_GLOBAL]]></Declaration>";
        using var file = TemporaryFile.Create("Globals.TcGVL", xml);

        var exception = Assert.Throws<SourceDocumentException>(() => SourceDocumentLoader.Load(file.Path));

        Assert.Contains("not well formed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiscoverySkipsGeneratedAndBuildDirectories()
    {
        using var directory = new TemporaryDirectory();
        var included = directory.Write("PLC/Main.TcPOU", string.Empty);
        directory.Write("PLC/Types.TcDUT", string.Empty);
        directory.Write("PLC/_Boot/Generated.TcPOU", string.Empty);
        directory.Write("PLC/bin/Output.st", string.Empty);
        directory.Write("PLC/Readme.md", string.Empty);

        var files = SourceFileDiscovery.Discover([directory.Path]);

        Assert.Equal(2, files.Count);
        Assert.Contains(included, files);
    }

    [Fact]
    public void AtomicWriterReplacesFileAndPreservesEncodingKind()
    {
        var originalBytes = Encoding.UTF8.Preamble.ToArray().Concat(Encoding.UTF8.GetBytes("old")).ToArray();
        using var file = TemporaryFile.Create("Program.st", originalBytes);
        var document = SourceDocumentLoader.Load(file.Path) with { Text = "new" };

        AtomicSourceFileWriter.Write(document);

        Assert.Equal(Encoding.UTF8.Preamble.ToArray().Concat(Encoding.UTF8.GetBytes("new")).ToArray(), File.ReadAllBytes(file.Path));
    }

    private sealed class TemporaryFile : IDisposable
    {
        private TemporaryFile(string path, byte[] bytes)
        {
            Path = path;
            Bytes = bytes;
        }

        public string Path { get; }

        public byte[] Bytes { get; }

        public static TemporaryFile Create(string fileName, string content) =>
            Create(fileName, new UTF8Encoding(false).GetBytes(content));

        public static TemporaryFile Create(string fileName, byte[] bytes)
        {
            var directory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"tc-format-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, fileName);
            File.WriteAllBytes(path, bytes);
            return new TemporaryFile(path, bytes);
        }

        public void Dispose()
        {
            Directory.Delete(System.IO.Path.GetDirectoryName(Path)!, recursive: true);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tc-format-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Write(string relativePath, string content)
        {
            var path = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

