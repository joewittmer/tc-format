namespace TcFormat.Core;

public sealed record ConfigurationDiagnostic(
    string Message,
    string? SourcePath = null,
    string? PropertyName = null)
{
    public override string ToString()
    {
        var location = SourcePath is null ? string.Empty : $"{SourcePath}: ";
        var property = PropertyName is null ? string.Empty : $"{PropertyName}: ";
        return $"{location}{property}{Message}";
    }
}

public sealed record ResolvedOptionValue(
    string Name,
    string Value,
    string Source,
    string? UnsetBy = null);

public sealed record ResolvedFormatterConfiguration(
    string FilePath,
    FormatterOptions Options,
    IReadOnlyList<ResolvedOptionValue> Values,
    IReadOnlyList<ConfigurationDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

