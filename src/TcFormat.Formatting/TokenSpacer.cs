using TcFormat.Core;
using TcFormat.Syntax;

namespace TcFormat.Formatting;

internal static class TokenSpacer
{
    public static IReadOnlyList<SyntaxToken> Apply(IReadOnlyList<SyntaxToken> tokens, FormatterOptions options)
    {
        var output = new List<SyntaxToken>(tokens.Count);
        var line = new List<SyntaxToken>();
        var delimiterDepth = 0;

        foreach (var token in tokens)
        {
            if (token.Kind != SyntaxKind.NewLine)
            {
                line.Add(token);
                continue;
            }

            FormatLine(line, output, options, ref delimiterDepth);
            output.Add(token);
            line.Clear();
        }

        FormatLine(line, output, options, ref delimiterDepth);
        return output;
    }

    private static void FormatLine(
        IReadOnlyList<SyntaxToken> line,
        ICollection<SyntaxToken> output,
        FormatterOptions options,
        ref int delimiterDepth)
    {
        if (line.Count == 0)
        {
            return;
        }

        if (line.Any(token => token.Text.AsSpan().IndexOfAny('\r', '\n') >= 0))
        {
            foreach (var token in line)
            {
                output.Add(token);
            }

            UpdateDelimiterDepth(line, ref delimiterDepth);
            return;
        }

        var leadingWhitespace = string.Empty;
        var pendingWhitespace = string.Empty;
        var items = new List<LineItem>();

        foreach (var token in line)
        {
            if (token.Kind == SyntaxKind.Whitespace)
            {
                pendingWhitespace += token.Text;
                continue;
            }

            if (items.Count == 0)
            {
                leadingWhitespace = pendingWhitespace;
            }

            items.Add(new LineItem(token, pendingWhitespace, delimiterDepth));
            pendingWhitespace = string.Empty;
            delimiterDepth += token.Text switch
            {
                "(" or "[" => 1,
                ")" or "]" => -1,
                _ => 0
            };
        }

        if (items.Count == 0)
        {
            foreach (var token in line)
            {
                output.Add(token);
            }

            return;
        }

        if (leadingWhitespace.Length > 0)
        {
            output.Add(CreateWhitespace(leadingWhitespace, items[0].Token));
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (index > 0)
            {
                var spaces = GetSpaces(items, index, options);
                if (spaces > 0)
                {
                    output.Add(CreateWhitespace(new string(' ', spaces), items[index].Token));
                }
            }

            output.Add(items[index].Token);
        }

        if (pendingWhitespace.Length > 0 && !options.File.TrimTrailingWhitespace)
        {
            output.Add(CreateWhitespace(pendingWhitespace, items[^1].Token));
        }
    }

    private static int GetSpaces(IReadOnlyList<LineItem> items, int currentIndex, FormatterOptions options)
    {
        var previousIndex = currentIndex - 1;
        var previous = items[previousIndex];
        var current = items[currentIndex];

        if (current.Token.Kind == SyntaxKind.LineComment)
        {
            return options.Spacing.SpacesBeforeEndOfLineComment;
        }

        if (previous.Token.Kind is SyntaxKind.LineComment)
        {
            return 0;
        }

        if (current.Token.Kind is SyntaxKind.BlockComment or SyntaxKind.Pragma ||
            previous.Token.Kind is SyntaxKind.BlockComment or SyntaxKind.Pragma)
        {
            return current.WhitespaceBefore.Length;
        }

        if (current.Token.Text == "," || current.Token.Text == ";" || current.Token.Text == ".")
        {
            return 0;
        }

        if (previous.Token.Text == ".")
        {
            return 0;
        }

        if (previous.Token.Text == ",")
        {
            return options.Spacing.AfterComma ? 1 : 0;
        }

        if (previous.Token.Text == ";")
        {
            return 1;
        }

        if (current.Token.Text == ")")
        {
            return previous.Token.Text == "("
                ? 0
                : options.Spacing.InsideParentheses ? 1 : 0;
        }

        if (previous.Token.Text == "(")
        {
            return current.Token.Text == ")"
                ? 0
                : options.Spacing.InsideParentheses ? 1 : 0;
        }

        if (current.Token.Text == "]")
        {
            return previous.Token.Text == "["
                ? 0
                : options.Spacing.InsideBrackets ? 1 : 0;
        }

        if (previous.Token.Text == "[")
        {
            return current.Token.Text == "]"
                ? 0
                : options.Spacing.InsideBrackets ? 1 : 0;
        }

        if (current.Token.Text is "(" or "[")
        {
            return IsOperator(previous.Token.Text) &&
                   ShouldSpaceOperator(previous.Token.Text, previous.DelimiterDepth, options)
                ? 1
                : 0;
        }

        if (previous.Token.Text is ")" or "]" && IsWordLike(current.Token))
        {
            return 1;
        }

        if (IsLabelColon(items, currentIndex))
        {
            return 0;
        }

        if (current.Token.Text == ":")
        {
            return options.Spacing.BeforeDeclarationColon ? 1 : 0;
        }

        if (previous.Token.Text == ":")
        {
            return IsLabelColon(items, previousIndex)
                ? 0
                : options.Spacing.AfterDeclarationColon ? 1 : 0;
        }

        if (IsUnarySign(items, previousIndex))
        {
            return 0;
        }

        if (IsOperator(current.Token.Text))
        {
            return ShouldSpaceOperator(current.Token.Text, current.DelimiterDepth, options) ? 1 : 0;
        }

        if (IsOperator(previous.Token.Text))
        {
            return ShouldSpaceOperator(previous.Token.Text, previous.DelimiterDepth, options) ? 1 : 0;
        }

        return NeedsSeparatingSpace(previous.Token, current.Token) ? 1 : 0;
    }

    private static bool IsLabelColon(IReadOnlyList<LineItem> items, int index)
    {
        if (items[index].Token.Text != ":")
        {
            return false;
        }

        return index == items.Count - 1 ||
               items.Skip(index + 1).All(item => item.Token.Kind is SyntaxKind.LineComment or SyntaxKind.BlockComment);
    }

    private static bool IsUnarySign(IReadOnlyList<LineItem> items, int index)
    {
        if (index < 0 || items[index].Token.Text is not ("+" or "-"))
        {
            return false;
        }

        if (index == 0)
        {
            return true;
        }

        var previous = items[index - 1].Token.Text;
        return IsOperator(previous) || previous is "(" or "[" or ",";
    }

    private static bool IsOperator(string text) => text is
        ":=" or "=>" or "REF=" or "?=" or
        "=" or "<" or ">" or "<=" or ">=" or "<>" or
        "+" or "-" or "*" or "/" or "**" or "&" or ".." or "^";

    private static bool ShouldSpaceOperator(string text, int delimiterDepth, FormatterOptions options) => text switch
    {
        ":=" when delimiterDepth > 0 => options.Spacing.AroundNamedArgumentOperators,
        ":=" or "REF=" => options.Spacing.AroundAssignmentOperators,
        "=>" => options.Spacing.AroundNamedArgumentOperators,
        "=" or "<" or ">" or "<=" or ">=" or "<>" or "?=" =>
            options.Spacing.AroundComparisonOperators,
        ".." => options.Spacing.AroundRangeOperator,
        "^" => false,
        _ => options.Spacing.AroundBinaryOperators
    };

    private static bool NeedsSeparatingSpace(SyntaxToken previous, SyntaxToken current) =>
        IsWordLike(previous) && IsWordLike(current);

    private static bool IsWordLike(SyntaxToken token) => token.Kind is
        SyntaxKind.Identifier or
        SyntaxKind.Keyword or
        SyntaxKind.NumberLiteral or
        SyntaxKind.StringLiteral or
        SyntaxKind.DirectAddress;

    private static SyntaxToken CreateWhitespace(string text, SyntaxToken anchor) =>
        new(SyntaxKind.Whitespace, text, anchor.Offset, anchor.Line, anchor.Column);

    private static void UpdateDelimiterDepth(IEnumerable<SyntaxToken> tokens, ref int delimiterDepth)
    {
        foreach (var token in tokens.Where(token => token.Kind is not SyntaxKind.BlockComment and not SyntaxKind.LineComment))
        {
            delimiterDepth += token.Text switch
            {
                "(" or "[" => 1,
                ")" or "]" => -1,
                _ => 0
            };
        }
    }

    private sealed record LineItem(SyntaxToken Token, string WhitespaceBefore, int DelimiterDepth);
}

