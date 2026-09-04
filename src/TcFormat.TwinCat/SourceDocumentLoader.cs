using System.Text;
using System.Xml;

namespace TcFormat.TwinCat;

public static class SourceDocumentLoader
{
    public static SourceDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);

        if (!SourceFileTypes.IsSupported(fullPath))
        {
            throw new SourceDocumentException($"{fullPath}: unsupported source file extension.");
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SourceDocumentException($"{fullPath}: could not read source file.", exception);
        }

        try
        {
            var encoding = SourceTextEncoding.Detect(bytes, fullPath);
            var text = encoding.Decode(bytes);
            if (SourceFileTypes.IsTwinCatXml(fullPath))
            {
                ValidateXml(text, fullPath);
            }

            var regions = SourceFileTypes.IsPlainText(fullPath)
                ? [new CodeRegion(CodeRegionKind.PlainText, 0, text.Length)]
                : TwinCatCodeRegionExtractor.Extract(text);

            if (regions.Count == 0)
            {
                throw new SourceDocumentException(
                    $"{fullPath}: no supported Declaration or ST CDATA regions were found.");
            }

            return new SourceDocument(fullPath, text, encoding, regions);
        }
        catch (DecoderFallbackException exception)
        {
            throw new SourceDocumentException($"{fullPath}: source file contains invalid encoded text.", exception);
        }
    }

    private static void ValidateXml(string text, string path)
    {
        try
        {
            using var textReader = new StringReader(text);
            using var xmlReader = XmlReader.Create(
                textReader,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });

            while (xmlReader.Read())
            {
            }
        }
        catch (XmlException exception)
        {
            throw new SourceDocumentException($"{path}: TwinCAT XML is not well formed.", exception);
        }
    }
}

