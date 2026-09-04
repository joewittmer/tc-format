namespace TcFormat.Syntax;

public static class StructuredTextLexer
{
    private static readonly string[] MultiCharacterOperators =
    [
        "REF=", ":=", "=>", "<=", ">=", "<>", "**", "..", "?="
    ];

    public static LexResult Lex(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var lexer = new Lexer(source);
        return lexer.Run();
    }

    private sealed class Lexer(string source)
    {
        private readonly List<SyntaxToken> tokens = [];
        private readonly List<SyntaxDiagnostic> diagnostics = [];
        private int offset;
        private int line = 1;
        private int column = 1;

        public LexResult Run()
        {
            while (offset < source.Length)
            {
                var start = Mark();
                var current = source[offset];

                if (current is ' ' or '\t' or '\f' or '\v')
                {
                    ScanHorizontalWhitespace(start);
                }
                else if (current is '\r' or '\n')
                {
                    ScanNewLine(start);
                }
                else if (StartsWith("//"))
                {
                    ScanLineComment(start);
                }
                else if (StartsWith("(*"))
                {
                    ScanBlockComment(start);
                }
                else if (current is '\'' or '"')
                {
                    ScanString(start, current);
                }
                else if (current == '{')
                {
                    ScanPragma(start);
                }
                else if (current == '%')
                {
                    ScanDirectAddress(start);
                }
                else if (TryScanOperator(start))
                {
                }
                else if (char.IsLetter(current) || current == '_')
                {
                    ScanWord(start);
                }
                else if (char.IsDigit(current))
                {
                    ScanNumber(start);
                }
                else if (IsPunctuation(current))
                {
                    Advance();
                    AddToken(SyntaxKind.Punctuation, start);
                }
                else
                {
                    Advance();
                    AddToken(SyntaxKind.Unknown, start);
                }
            }

            return new LexResult(tokens, diagnostics);
        }

        private void ScanHorizontalWhitespace(Position start)
        {
            while (offset < source.Length && source[offset] is ' ' or '\t' or '\f' or '\v')
            {
                Advance();
            }

            AddToken(SyntaxKind.Whitespace, start);
        }

        private void ScanNewLine(Position start)
        {
            if (StartsWith("\r\n"))
            {
                offset += 2;
            }
            else
            {
                offset++;
            }

            line++;
            column = 1;
            AddToken(SyntaxKind.NewLine, start);
        }

        private void ScanLineComment(Position start)
        {
            Advance(2);
            while (offset < source.Length && source[offset] is not '\r' and not '\n')
            {
                Advance();
            }

            AddToken(SyntaxKind.LineComment, start);
        }

        private void ScanBlockComment(Position start)
        {
            var depth = 1;
            Advance(2);

            while (offset < source.Length && depth > 0)
            {
                if (StartsWith("(*"))
                {
                    depth++;
                    Advance(2);
                }
                else if (StartsWith("*)"))
                {
                    depth--;
                    Advance(2);
                }
                else
                {
                    Advance();
                }
            }

            AddToken(SyntaxKind.BlockComment, start);
            if (depth > 0)
            {
                diagnostics.Add(new SyntaxDiagnostic(
                    "Unterminated block comment.",
                    start.Offset,
                    start.Line,
                    start.Column));
            }
        }

        private void ScanString(Position start, char delimiter)
        {
            var terminated = false;
            Advance();

            while (offset < source.Length)
            {
                if (source[offset] is '\r' or '\n')
                {
                    break;
                }

                if (source[offset] == '$' && offset + 1 < source.Length)
                {
                    Advance(2);
                    continue;
                }

                if (source[offset] != delimiter)
                {
                    Advance();
                    continue;
                }

                if (offset + 1 < source.Length && source[offset + 1] == delimiter)
                {
                    Advance(2);
                    continue;
                }

                Advance();
                terminated = true;
                break;
            }

            AddToken(SyntaxKind.StringLiteral, start);
            if (!terminated)
            {
                diagnostics.Add(new SyntaxDiagnostic(
                    "Unterminated string literal.",
                    start.Offset,
                    start.Line,
                    start.Column));
            }
        }

        private void ScanPragma(Position start)
        {
            var terminated = false;
            Advance();

            while (offset < source.Length)
            {
                if (source[offset] == '}')
                {
                    Advance();
                    terminated = true;
                    break;
                }

                Advance();
            }

            AddToken(SyntaxKind.Pragma, start);
            if (!terminated)
            {
                diagnostics.Add(new SyntaxDiagnostic(
                    "Unterminated pragma.",
                    start.Offset,
                    start.Line,
                    start.Column));
            }
        }

        private void ScanDirectAddress(Position start)
        {
            Advance();
            while (offset < source.Length &&
                   !char.IsWhiteSpace(source[offset]) &&
                   source[offset] is not ':' and not ';' and not ',' and not ')' and not ']')
            {
                Advance();
            }

            AddToken(SyntaxKind.DirectAddress, start);
        }

        private void ScanWord(Position start)
        {
            Advance();
            while (offset < source.Length && (char.IsLetterOrDigit(source[offset]) || source[offset] == '_'))
            {
                Advance();
            }

            var text = source[start.Offset..offset];
            AddToken(KeywordFacts.IsKeyword(text) ? SyntaxKind.Keyword : SyntaxKind.Identifier, start);
        }

        private void ScanNumber(Position start)
        {
            Advance();
            while (offset < source.Length)
            {
                var current = source[offset];
                if (char.IsLetterOrDigit(current) || current is '_' or '#')
                {
                    Advance();
                    continue;
                }

                if (current == '.' && !StartsWith(".."))
                {
                    Advance();
                    continue;
                }

                if (current is '+' or '-' && offset > start.Offset && source[offset - 1] is 'e' or 'E')
                {
                    Advance();
                    continue;
                }

                break;
            }

            AddToken(SyntaxKind.NumberLiteral, start);
        }

        private bool TryScanOperator(Position start)
        {
            foreach (var candidate in MultiCharacterOperators)
            {
                if (!StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Advance(candidate.Length);
                AddToken(SyntaxKind.Operator, start);
                return true;
            }

            if (source[offset] is ':' or '=' or '<' or '>' or '+' or '-' or '*' or '/' or '&' or '^')
            {
                Advance();
                AddToken(SyntaxKind.Operator, start);
                return true;
            }

            return false;
        }

        private static bool IsPunctuation(char value) => value is ';' or ',' or '.' or '(' or ')' or '[' or ']';

        private bool StartsWith(string value, StringComparison comparison = StringComparison.Ordinal) =>
            source.AsSpan(offset).StartsWith(value, comparison);

        private void Advance(int count = 1)
        {
            for (var index = 0; index < count && offset < source.Length; index++)
            {
                if (source[offset] == '\r')
                {
                    if (offset + 1 < source.Length && source[offset + 1] == '\n')
                    {
                        offset++;
                    }

                    line++;
                    column = 1;
                    offset++;
                }
                else if (source[offset] == '\n')
                {
                    line++;
                    column = 1;
                    offset++;
                }
                else
                {
                    offset++;
                    column++;
                }
            }
        }

        private Position Mark() => new(offset, line, column);

        private void AddToken(SyntaxKind kind, Position start) =>
            tokens.Add(new SyntaxToken(kind, source[start.Offset..offset], start.Offset, start.Line, start.Column));

        private readonly record struct Position(int Offset, int Line, int Column);
    }
}
