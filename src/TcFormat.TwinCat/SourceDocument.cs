namespace TcFormat.TwinCat;

public enum CodeRegionKind
{
    PlainText,
    Declaration,
    Implementation
}

public sealed record CodeRegion(CodeRegionKind Kind, int Start, int Length)
{
    public int End => Start + Length;
}

public sealed record SourceDocument(
    string Path,
    string Text,
    SourceTextEncoding Encoding,
    IReadOnlyList<CodeRegion> Regions)
{
    public byte[] Encode() => Encoding.Encode(Text);

    public string GetRegionText(CodeRegion region) => Text.Substring(region.Start, region.Length);

    public SourceDocument ReplaceRegions(IReadOnlyList<string> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        if (replacements.Count != Regions.Count)
        {
            throw new ArgumentException(
                $"Expected {Regions.Count} replacement region(s), received {replacements.Count}.",
                nameof(replacements));
        }

        var updated = Text;
        for (var index = Regions.Count - 1; index >= 0; index--)
        {
            var region = Regions[index];
            updated = string.Concat(updated.AsSpan(0, region.Start), replacements[index], updated.AsSpan(region.End));
        }

        return this with { Text = updated };
    }
}

public sealed class SourceDocumentException(string message, Exception? innerException = null)
    : Exception(message, innerException);

