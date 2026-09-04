namespace TcFormat.Syntax;

public enum SyntaxKind
{
    Whitespace,
    NewLine,
    Identifier,
    Keyword,
    NumberLiteral,
    StringLiteral,
    LineComment,
    BlockComment,
    Pragma,
    DirectAddress,
    Operator,
    Punctuation,
    Unknown
}

public sealed record SyntaxToken(
    SyntaxKind Kind,
    string Text,
    int Offset,
    int Line,
    int Column)
{
    public bool IsTrivia => Kind is
        SyntaxKind.Whitespace or
        SyntaxKind.NewLine or
        SyntaxKind.LineComment or
        SyntaxKind.BlockComment;
}

public sealed record SyntaxDiagnostic(string Message, int Offset, int Line, int Column);

public sealed record LexResult(
    IReadOnlyList<SyntaxToken> Tokens,
    IReadOnlyList<SyntaxDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;

    public string ReconstructSource() => string.Concat(Tokens.Select(token => token.Text));
}

