using TcFormat.Core;
using TcFormat.Syntax;

namespace TcFormat.Formatting;

internal static class CompactControlHeaders
{
    public static IReadOnlyList<SyntaxToken> Apply(IReadOnlyList<SyntaxToken> tokens, FormatterOptions options)
    {
        if (!options.Wrapping.CompactControlFlowHeaders)
        {
            return tokens;
        }

        var output = new List<SyntaxToken>();
        var depth = 0;
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            var terminator = depth == 0 && token.Kind == SyntaxKind.Keyword
                ? token.Text.ToUpperInvariant() switch
                {
                    "IF" or "ELSIF" => "THEN",
                    "FOR" or "WHILE" => "DO",
                    "CASE" => "OF",
                    _ => null
                }
                : null;
            var end = terminator is null ? -1 : FindHeaderEnd(tokens, index, terminator);
            if (end >= 0)
            {
                var header = tokens.Skip(index).Take(end - index + 1).ToArray();
                if (header.Any(item => item.Kind == SyntaxKind.NewLine))
                {
                    var indentation = index > 0 && tokens[index - 1].Kind == SyntaxKind.Whitespace
                        ? tokens[index - 1].Text : "";
                    output.AddRange(FormatHeader(header, indentation, options));
                }
                else
                {
                    output.AddRange(header);
                }
                index = end;
                continue;
            }
            output.Add(token);
            if (!token.IsTrivia)
            {
                depth += DelimiterChange(token);
            }
        }
        return output;
    }

    private static int FindHeaderEnd(IReadOnlyList<SyntaxToken> tokens, int start, string terminator)
    {
        var depth = 0;
        for (var index = start + 1; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.IsTrivia || token.Kind == SyntaxKind.Pragma)
            {
                continue;
            }
            depth += DelimiterChange(token);
            if (depth == 0 && token.Kind == SyntaxKind.Keyword &&
                token.Text.Equals(terminator, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
            if (depth == 0 && token.Text == ";")
            {
                break;
            }
        }
        return -1;
    }

    private static IReadOnlyList<SyntaxToken> FormatHeader(
        IReadOnlyList<SyntaxToken> header, string indentation, FormatterOptions options)
    {
        var tokens = MoveLogicalOperators(header);
        var scopes = FindScopes(tokens);
        var openingScopes = scopes.ToDictionary(scope => scope.Open);
        var closingScopes = scopes.ToDictionary(scope => scope.Close);
        var stack = new Stack<Scope>();
        var output = new List<SyntaxToken>();
        var previous = -1;
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Kind is SyntaxKind.Whitespace or SyntaxKind.NewLine)
            {
                continue;
            }

            string? gap = null;
            if (previous >= 0 && !IsBarrier(tokens[previous]) && !IsBarrier(token))
            {
                if (openingScopes.TryGetValue(previous, out var opening) && opening.Multiline)
                {
                    gap = opening.Call ? "\n" : " ";
                }
                else if (tokens[previous].Text == "," && stack.TryPeek(out var parent) && parent is { Call: true, Multiline: true })
                {
                    gap = "\n";
                }
                else if (closingScopes.TryGetValue(index, out var closing) && closing.Multiline)
                {
                    gap = closing.Call && options.Spacing.InsideParentheses ? " " : "";
                }
                else if (index == tokens.Count - 1)
                {
                    gap = " ";
                }
            }

            if (gap is null)
            {
                output.AddRange(tokens.Skip(previous + 1).Take(index - previous - 1));
            }
            else if (gap.Length > 0)
            {
                output.Add(new SyntaxToken(gap == "\n" ? SyntaxKind.NewLine : SyntaxKind.Whitespace,
                    gap, token.Offset, token.Line, -1));
            }
            output.Add(token);
            if (openingScopes.TryGetValue(index, out var scope))
            {
                stack.Push(scope);
            }
            else if (closingScopes.ContainsKey(index) && stack.Count > 0)
            {
                stack.Pop();
            }
            previous = index;
        }
        return Indent(output, indentation, options);
    }

    private static List<SyntaxToken> MoveLogicalOperators(IReadOnlyList<SyntaxToken> header)
    {
        var output = header.ToList();
        for (var index = 1; index < output.Count; index++)
        {
            var token = output[index];
            if (token.Kind != SyntaxKind.Keyword || token.Text.ToUpperInvariant() is not
                ("AND" or "AND_THEN" or "OR" or "OR_ELSE" or "XOR"))
            {
                continue;
            }
            var left = index - 1;
            var hasBreak = false;
            while (left >= 0 && output[left].Kind is SyntaxKind.Whitespace or SyntaxKind.NewLine)
            {
                hasBreak |= output[left].Kind == SyntaxKind.NewLine;
                left--;
            }
            if (!hasBreak || left < 0 || IsBarrier(output[left]))
            {
                continue;
            }
            var right = index + 1;
            while (right < output.Count && output[right].Kind == SyntaxKind.Whitespace)
            {
                right++;
            }
            if (right == output.Count || output[right].Kind == SyntaxKind.NewLine || IsBarrier(output[right]))
            {
                continue;
            }
            output.RemoveRange(left + 1, right - left - 1);
            output.InsertRange(left + 1,
            [
                new SyntaxToken(SyntaxKind.Whitespace, " ", token.Offset, token.Line, -1),
                token,
                new SyntaxToken(SyntaxKind.NewLine, "\n", token.Offset, token.Line, -1)
            ]);
            index = left + 3;
        }
        return output;
    }

    private static IReadOnlyList<Scope> FindScopes(IReadOnlyList<SyntaxToken> tokens)
    {
        var output = new List<Scope>();
        var stack = new Stack<(int Index, bool Call, int Line)>();
        SyntaxToken? previous = null;
        var line = 0;
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Kind == SyntaxKind.NewLine)
            {
                line++;
            }
            if (token.IsTrivia || token.Kind == SyntaxKind.Pragma)
            {
                continue;
            }
            if (token.Text is "(" or "[")
            {
                var call = token.Text == "(" && previous is not null &&
                    (previous.Kind == SyntaxKind.Identifier || previous.Text is ")" or "]" or "^");
                stack.Push((index, call, line));
            }
            else if (token.Text is ")" or "]" && stack.TryPop(out var opening))
            {
                output.Add(new Scope(opening.Index, index, opening.Call, line > opening.Line));
            }
            previous = token;
        }
        return output;
    }

    private static IReadOnlyList<SyntaxToken> Indent(IReadOnlyList<SyntaxToken> tokens, string indentation, FormatterOptions options)
    {
        var scopes = FindScopes(tokens);
        var openings = scopes.ToDictionary(scope => scope.Open);
        var closings = scopes.ToDictionary(scope => scope.Close);
        var stack = new Stack<Scope>();
        var output = new List<SyntaxToken>();
        var unit = options.Indentation.Style == IndentStyle.Tabs && options.Indentation.ContinuationSize == options.Indentation.TabWidth
            ? "\t" : new string(' ', options.Indentation.ContinuationSize);
        var lineStart = false;
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (lineStart && token.Kind == SyntaxKind.Whitespace)
            {
                continue;
            }
            if (lineStart && token.Kind != SyntaxKind.NewLine)
            {
                var levels = 1 + stack.Count(scope => scope.Multiline);
                for (var closing = index; closing < tokens.Count; closing++)
                {
                    if (tokens[closing].Kind == SyntaxKind.Whitespace)
                    {
                        continue;
                    }
                    if (!closings.TryGetValue(closing, out var scope))
                    {
                        break;
                    }
                    levels -= scope.Multiline ? 1 : 0;
                }
                if (index == tokens.Count - 1)
                {
                    levels = 0;
                }
                output.Add(new SyntaxToken(SyntaxKind.Whitespace,
                    indentation + string.Concat(Enumerable.Repeat(unit, Math.Max(0, levels))), token.Offset, token.Line, -1));
                lineStart = false;
            }
            output.Add(token);
            if (openings.TryGetValue(index, out var opening))
            {
                stack.Push(opening);
            }
            else if (closings.ContainsKey(index) && stack.Count > 0)
            {
                stack.Pop();
            }
            lineStart |= token.Kind == SyntaxKind.NewLine;
        }
        return output;
    }

    private static bool IsBarrier(SyntaxToken token) =>
        token.Kind is SyntaxKind.LineComment or SyntaxKind.BlockComment or SyntaxKind.Pragma;

    private static int DelimiterChange(SyntaxToken token) => token.Text switch
    {
        "(" or "[" => 1,
        ")" or "]" => -1,
        _ => 0
    };

    private sealed record Scope(int Open, int Close, bool Call, bool Multiline);
}
