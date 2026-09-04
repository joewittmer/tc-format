namespace TcFormat.TwinCat;

public static class TwinCatCodeRegionExtractor
{
    private const string CDataStart = "<![CDATA[";
    private const string CDataEnd = "]]>";

    public static IReadOnlyList<CodeRegion> Extract(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        var regions = new List<CodeRegion>();
        var searchStart = 0;

        while (searchStart < xml.Length)
        {
            var cdataStart = xml.IndexOf(CDataStart, searchStart, StringComparison.Ordinal);
            if (cdataStart < 0)
            {
                break;
            }

            var contentStart = cdataStart + CDataStart.Length;
            var cdataEnd = xml.IndexOf(CDataEnd, contentStart, StringComparison.Ordinal);
            if (cdataEnd < 0)
            {
                throw new SourceDocumentException("TwinCAT XML contains an unterminated CDATA section.");
            }

            var elementName = FindContainingElementName(xml, cdataStart);
            var kind = elementName switch
            {
                "Declaration" => CodeRegionKind.Declaration,
                "ST" => CodeRegionKind.Implementation,
                _ => (CodeRegionKind?)null
            };

            if (kind.HasValue)
            {
                regions.Add(new CodeRegion(kind.Value, contentStart, cdataEnd - contentStart));
            }

            searchStart = cdataEnd + CDataEnd.Length;
        }

        return regions;
    }

    private static string? FindContainingElementName(string xml, int cdataStart)
    {
        var tagEnd = cdataStart - 1;
        while (tagEnd >= 0 && char.IsWhiteSpace(xml[tagEnd]))
        {
            tagEnd--;
        }

        if (tagEnd < 0 || xml[tagEnd] != '>')
        {
            return null;
        }

        var tagStart = xml.LastIndexOf('<', tagEnd);
        if (tagStart < 0 || tagStart + 1 >= tagEnd || xml[tagStart + 1] is '/' or '!' or '?')
        {
            return null;
        }

        var nameStart = tagStart + 1;
        var nameEnd = nameStart;
        while (nameEnd < tagEnd &&
               !char.IsWhiteSpace(xml[nameEnd]) &&
               xml[nameEnd] is not '/' and not '>')
        {
            nameEnd++;
        }

        var qualifiedName = xml[nameStart..nameEnd];
        var colon = qualifiedName.LastIndexOf(':');
        return colon >= 0 ? qualifiedName[(colon + 1)..] : qualifiedName;
    }
}

