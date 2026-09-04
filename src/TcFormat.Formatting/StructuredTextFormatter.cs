using System.Text;

using TcFormat.Core;
using TcFormat.Syntax;

namespace TcFormat.Formatting;

public static class StructuredTextFormatter
{
    public static FormatResult Format(string source, FormatterOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        var optionErrors = options.Validate();
        if (optionErrors.Count > 0)
        {
            return new FormatResult(
                source,
                source,
                optionErrors.Select(message => new FormatDiagnostic(message)).ToArray());
        }

        var original = StructuredTextLexer.Lex(source);
        if (!original.IsValid)
        {
            return new FormatResult(
                source,
                source,
                original.Diagnostics.Select(diagnostic => new FormatDiagnostic(
                    diagnostic.Message,
                    diagnostic.Line,
                    diagnostic.Column)).ToArray());
        }

        var statements = StatementSplitter.Apply(original.Tokens, options);
        var blankLines = BlankLineNormalizer.Apply(statements, options);
        var indented = StructuralIndenter.Apply(blankLines, options);
        if (indented.Diagnostics.Count > 0)
        {
            return new FormatResult(source, source, indented.Diagnostics);
        }

        var spaced = TokenSpacer.Apply(indented.Tokens, options);
        var wrapped = LineWrapper.Apply(spaced, options);
        var aligned = VerticalAligner.Apply(wrapped, options);
        var formattedText = Render(aligned, options);
        var formatted = StructuredTextLexer.Lex(formattedText);
        if (!formatted.IsValid)
        {
            return new FormatResult(
                source,
                source,
                formatted.Diagnostics.Select(diagnostic => new FormatDiagnostic(
                    $"Formatter produced invalid tokenization: {diagnostic.Message}",
                    diagnostic.Line,
                    diagnostic.Column)).ToArray());
        }

        if (!string.Equals(
                SemanticTokenFingerprint.Create(original.Tokens),
                SemanticTokenFingerprint.Create(formatted.Tokens),
                StringComparison.Ordinal))
        {
            return new FormatResult(
                source,
                source,
                [new FormatDiagnostic("Safety check failed: semantic token stream changed.")]);
        }

        return new FormatResult(source, formattedText, []);
    }

    private static string Render(IReadOnlyList<SyntaxToken> tokens, FormatterOptions options)
    {
        var output = new List<RenderedToken>(tokens.Count + 1);
        var pendingWhitespace = new StringBuilder();
        var consecutiveNewLines = 0;
        var maximumConsecutiveNewLines = options.Layout.MaximumConsecutiveBlankLines + 1;
        var newLine = GetNewLine(options.File.EndOfLine);

        foreach (var token in tokens)
        {
            if (token.Kind == SyntaxKind.Whitespace)
            {
                pendingWhitespace.Append(token.Text);
                continue;
            }

            if (token.Kind == SyntaxKind.NewLine)
            {
                if (consecutiveNewLines < maximumConsecutiveNewLines)
                {
                    if (!options.File.TrimTrailingWhitespace && pendingWhitespace.Length > 0)
                    {
                        output.Add(new RenderedToken(SyntaxKind.Whitespace, pendingWhitespace.ToString()));
                    }

                    output.Add(new RenderedToken(SyntaxKind.NewLine, newLine));
                }

                pendingWhitespace.Clear();
                consecutiveNewLines++;
                continue;
            }

            if (pendingWhitespace.Length > 0)
            {
                output.Add(new RenderedToken(SyntaxKind.Whitespace, pendingWhitespace.ToString()));
                pendingWhitespace.Clear();
            }

            output.Add(new RenderedToken(token.Kind, FormatTokenText(token, options)));
            consecutiveNewLines = 0;
        }

        if (pendingWhitespace.Length > 0 && !options.File.TrimTrailingWhitespace)
        {
            output.Add(new RenderedToken(SyntaxKind.Whitespace, pendingWhitespace.ToString()));
        }

        ApplyFinalNewLinePolicy(output, options.File.InsertFinalNewline, newLine);
        return string.Concat(output.Select(token => token.Text));
    }

    private static string FormatTokenText(SyntaxToken token, FormatterOptions options)
    {
        if (token.Kind != SyntaxKind.Keyword)
        {
            return token.Text;
        }

        return options.KeywordCase switch
        {
            KeywordCase.Upper => token.Text.ToUpperInvariant(),
            KeywordCase.Lower => token.Text.ToLowerInvariant(),
            KeywordCase.Preserve => token.Text,
            _ => token.Text
        };
    }

    private static void ApplyFinalNewLinePolicy(List<RenderedToken> output, bool insert, string newLine)
    {
        if (insert)
        {
            if (output.Count == 0 || output[^1].Kind != SyntaxKind.NewLine)
            {
                output.Add(new RenderedToken(SyntaxKind.NewLine, newLine));
            }

            return;
        }

        while (output.Count > 0 && output[^1].Kind is SyntaxKind.NewLine or SyntaxKind.Whitespace)
        {
            output.RemoveAt(output.Count - 1);
        }
    }

    private static string GetNewLine(EndOfLineStyle style) => style switch
    {
        EndOfLineStyle.CrLf => "\r\n",
        EndOfLineStyle.Lf => "\n",
        EndOfLineStyle.Cr => "\r",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, null)
    };

    private sealed record RenderedToken(SyntaxKind Kind, string Text);
}

