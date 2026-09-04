namespace TcFormat.Formatting;

public sealed record FormatDiagnostic(string Message, int? Line = null, int? Column = null)
{
    public override string ToString() => Line.HasValue && Column.HasValue
        ? $"{Line}:{Column}: {Message}"
        : Message;
}

public sealed record FormatResult(
    string OriginalText,
    string FormattedText,
    IReadOnlyList<FormatDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;

    public bool Changed => !string.Equals(OriginalText, FormattedText, StringComparison.Ordinal);
}

