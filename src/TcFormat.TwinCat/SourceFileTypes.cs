namespace TcFormat.TwinCat;

public static class SourceFileTypes
{
    private static readonly HashSet<string> PlainTextExtensions = new(
        [".st", ".iecst"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> TwinCatXmlExtensions = new(
        [".tcpou", ".tcdut", ".tcgvl", ".tcitf", ".tcprg"],
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> SupportedExtensions { get; } =
        PlainTextExtensions.Concat(TwinCatXmlExtensions).Order().ToArray();

    public static bool IsSupported(string path) =>
        PlainTextExtensions.Contains(Path.GetExtension(path)) ||
        TwinCatXmlExtensions.Contains(Path.GetExtension(path));

    public static bool IsPlainText(string path) => PlainTextExtensions.Contains(Path.GetExtension(path));

    public static bool IsTwinCatXml(string path) => TwinCatXmlExtensions.Contains(Path.GetExtension(path));
}

