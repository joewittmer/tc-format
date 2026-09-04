using TcFormat.Core;
using TcFormat.Syntax;

namespace TcFormat.Formatting;

internal static class StatementSplitter
{
    public static IReadOnlyList<SyntaxToken> Apply(
        IReadOnlyList<SyntaxToken> tokens,
        FormatterOptions options)
    {
        if (!options.Layout.OneStatementPerLine)
        {
            return tokens;
        }

        var output = new List<SyntaxToken>(tokens.Count);
        var delimiterDepth = 0;

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            output.Add(token);

            if (token.Kind is not SyntaxKind.LineComment and not SyntaxKind.BlockComment)
            {
                delimiterDepth += token.Text switch
                {
                    "(" or "[" => 1,
                    ")" or "]" => -1,
                    _ => 0
                };
            }

            if (token.Text != ";" || delimiterDepth != 0 ||
                !TryFindNextStatement(tokens, index + 1, out var nextStatement, out var attachedEnd))
            {
                continue;
            }

            for (var attached = index + 1; attached < attachedEnd; attached++)
            {
                output.Add(tokens[attached]);
            }

            output.Add(CreateNewLine(tokens[nextStatement]));
            index = nextStatement - 1;
        }

        return output;
    }

    private static bool TryFindNextStatement(
        IReadOnlyList<SyntaxToken> tokens,
        int start,
        out int nextStatement,
        out int attachedEnd)
    {
        var cursor = start;
        attachedEnd = start;

        while (cursor < tokens.Count)
        {
            while (cursor < tokens.Count && tokens[cursor].Kind == SyntaxKind.Whitespace)
            {
                cursor++;
            }

            if (cursor >= tokens.Count || tokens[cursor].Kind is SyntaxKind.NewLine or SyntaxKind.LineComment)
            {
                nextStatement = -1;
                return false;
            }

            if (tokens[cursor].Kind != SyntaxKind.BlockComment)
            {
                nextStatement = cursor;
                return true;
            }

            cursor++;
            attachedEnd = cursor;
        }

        nextStatement = -1;
        return false;
    }

    private static SyntaxToken CreateNewLine(SyntaxToken anchor) =>
        new(SyntaxKind.NewLine, "\n", anchor.Offset, anchor.Line, anchor.Column);
}
