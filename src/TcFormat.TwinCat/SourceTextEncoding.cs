using System.Text;

namespace TcFormat.TwinCat;

public enum SourceEncodingKind
{
    Utf8,
    Utf8Bom,
    Utf16LittleEndian,
    Utf16BigEndian
}

public sealed class SourceTextEncoding
{
    private readonly Encoding encoding;

    private SourceTextEncoding(SourceEncodingKind kind, Encoding encoding, int preambleLength)
    {
        Kind = kind;
        this.encoding = encoding;
        PreambleLength = preambleLength;
    }

    public SourceEncodingKind Kind { get; }

    public int PreambleLength { get; }

    public static SourceTextEncoding Detect(ReadOnlySpan<byte> bytes, string path)
    {
        if (bytes.StartsWith(Encoding.UTF8.Preamble))
        {
            return new SourceTextEncoding(
                SourceEncodingKind.Utf8Bom,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true),
                Encoding.UTF8.Preamble.Length);
        }

        if (bytes.StartsWith(Encoding.Unicode.Preamble))
        {
            return new SourceTextEncoding(
                SourceEncodingKind.Utf16LittleEndian,
                new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true),
                Encoding.Unicode.Preamble.Length);
        }

        if (bytes.StartsWith(Encoding.BigEndianUnicode.Preamble))
        {
            return new SourceTextEncoding(
                SourceEncodingKind.Utf16BigEndian,
                new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true),
                Encoding.BigEndianUnicode.Preamble.Length);
        }

        ValidateBomlessXmlEncoding(bytes, path);
        return new SourceTextEncoding(
            SourceEncodingKind.Utf8,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            preambleLength: 0);
    }

    public string Decode(ReadOnlySpan<byte> bytes) => encoding.GetString(bytes[PreambleLength..]);

    public byte[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var body = encoding.GetBytes(text);
        if (PreambleLength == 0)
        {
            return body;
        }

        var preamble = encoding.GetPreamble();
        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    private static void ValidateBomlessXmlEncoding(ReadOnlySpan<byte> bytes, string path)
    {
        var prefixLength = Math.Min(bytes.Length, 256);
        var prefix = Encoding.ASCII.GetString(bytes[..prefixLength]);
        var declarationStart = prefix.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
        if (declarationStart < 0)
        {
            return;
        }

        var declarationEnd = prefix.IndexOf("?>", declarationStart, StringComparison.Ordinal);
        if (declarationEnd < 0)
        {
            return;
        }

        var declaration = prefix[declarationStart..declarationEnd];
        var encodingIndex = declaration.IndexOf("encoding", StringComparison.OrdinalIgnoreCase);
        if (encodingIndex < 0)
        {
            return;
        }

        var equalsIndex = declaration.IndexOf('=', encodingIndex);
        if (equalsIndex < 0)
        {
            return;
        }

        var valueStart = equalsIndex + 1;
        while (valueStart < declaration.Length && char.IsWhiteSpace(declaration[valueStart]))
        {
            valueStart++;
        }

        if (valueStart >= declaration.Length || declaration[valueStart] is not ('\'' or '"'))
        {
            return;
        }

        var quote = declaration[valueStart];
        var valueEnd = declaration.IndexOf(quote, valueStart + 1);
        if (valueEnd < 0)
        {
            return;
        }

        var declaredEncoding = declaration[(valueStart + 1)..valueEnd];
        if (!string.Equals(declaredEncoding, "utf-8", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(declaredEncoding, "utf8", StringComparison.OrdinalIgnoreCase))
        {
            throw new SourceDocumentException(
                $"{path}: unsupported XML encoding '{declaredEncoding}'. " +
                "Only UTF-8 or BOM-marked UTF-16 files are currently supported.");
        }
    }
}

