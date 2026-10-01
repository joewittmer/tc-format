using TcFormat.Core;
using TcFormat.Syntax;

namespace TcFormat.Formatting;

internal static class MultilineAssignments
{
    public static IReadOnlyList<Assignment> Find(IReadOnlyList<SyntaxToken> tokens)
    {
        var assignments = new List<Assignment>();
        var line = 0;
        var depth = 0;
        var candidate = -1;
        var assignmentOperator = -1;
        var startLine = 0;
        var declaration = false;
        var declarationDepth = 0;
        string? headerTerminator = null;
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Kind == SyntaxKind.NewLine)
            {
                line++;
                if (assignmentOperator < 0)
                {
                    candidate = -1;
                    declaration = false;
                }
                continue;
            }

            if (token.IsTrivia || token.Kind == SyntaxKind.Pragma)
            {
                continue;
            }

            if (depth == 0 && token.Kind == SyntaxKind.Keyword)
            {
                var keyword = token.Text.ToUpperInvariant();
                if (keyword is "TYPE" or "STRUCT" or "UNION" or "VAR" || keyword.StartsWith("VAR_", StringComparison.Ordinal))
                {
                    declarationDepth++;
                }
                else if (keyword is "END_TYPE" or "END_STRUCT" or "END_UNION" or "END_VAR")
                {
                    declarationDepth = Math.Max(0, declarationDepth - 1);
                }
                headerTerminator = keyword switch
                {
                    "IF" or "ELSIF" => "THEN",
                    "FOR" or "WHILE" => "DO",
                    "CASE" => "OF",
                    "UNTIL" => "END_REPEAT",
                    _ => headerTerminator
                };
                if (keyword == headerTerminator)
                {
                    headerTerminator = null;
                    candidate = -1;
                }
            }

            if (candidate < 0 && depth == 0)
            {
                candidate = index;
                startLine = line;
                declaration = token.Kind is not SyntaxKind.Identifier and not SyntaxKind.DirectAddress &&
                    token.Text.ToUpperInvariant() is not ("THIS" or "SUPER");
            }

            if (depth == 0)
            {
                declaration |= token.Text == ":";
                if (!declaration && declarationDepth == 0 && headerTerminator is null &&
                    assignmentOperator < 0 && token.Text.ToUpperInvariant() is ":=" or "REF=" or "?=")
                {
                    assignmentOperator = index;
                }
                if (token.Text == ";")
                {
                    if (assignmentOperator >= 0 && line > startLine)
                    {
                        assignments.Add(new Assignment(candidate, assignmentOperator, index, startLine, line));
                    }
                    candidate = -1;
                    assignmentOperator = -1;
                    declaration = false;
                }
            }

            depth += token.Text switch { "(" or "[" => 1, ")" or "]" => -1, _ => 0 };
        }
        return assignments;
    }

    public static IReadOnlyList<SyntaxToken> Align(IReadOnlyList<SyntaxToken> tokens, FormatterOptions options)
    {
        if (!options.Alignment.AssignmentContinuations)
        {
            return tokens;
        }

        var replacements = new Dictionary<int, (int End, string Indentation)>();
        foreach (var assignment in Find(tokens))
        {
            var rhs = assignment.Operator + 1;
            while (rhs < assignment.End &&
                   (tokens[rhs].Kind == SyntaxKind.Whitespace ||
                    tokens[rhs].Kind == SyntaxKind.BlockComment && tokens[rhs].Text.AsSpan().IndexOfAny('\r', '\n') < 0))
            {
                rhs++;
            }
            // A RHS on a new line uses normal continuation indentation. Calls
            // and initializers keep their own delimiter-based layout rules.
            if (tokens[rhs].IsTrivia || tokens[rhs].Kind == SyntaxKind.Pragma)
            {
                continue;
            }

            var firstLine = assignment.Start;
            while (firstLine > 0 && tokens[firstLine - 1].Kind != SyntaxKind.NewLine)
            {
                firstLine--;
            }
            var column = Width(tokens.Skip(firstLine).Take(rhs - firstLine), options.Indentation.TabWidth);
            var blockIndentation = tokens[firstLine].Kind == SyntaxKind.Whitespace ? tokens[firstLine].Text : "";
            var blockWidth = Width(tokens.Skip(firstLine).Take(tokens[firstLine].Kind == SyntaxKind.Whitespace ? 1 : 0),
                options.Indentation.TabWidth);
            var indentation = blockIndentation + new string(' ', Math.Max(0, column - blockWidth));
            var depth = 0;
            var shift = 0;
            for (var index = rhs; index <= assignment.End; index++)
            {
                var token = tokens[index];
                if (token.Kind == SyntaxKind.NewLine)
                {
                    var start = index + 1;
                    var content = start;
                    while (content < assignment.End && tokens[content].Kind == SyntaxKind.Whitespace)
                    {
                        content++;
                    }
                    if (tokens[content].Kind == SyntaxKind.NewLine || tokens[content].Text.AsSpan().IndexOfAny('\r', '\n') >= 0)
                    {
                        continue;
                    }
                    var width = Width(tokens.Skip(start).Take(content - start), options.Indentation.TabWidth);
                    if (depth == 0)
                    {
                        shift = column - width;
                        replacements[start] = (content, indentation);
                    }
                    else if (shift != 0)
                    {
                        replacements[start] = (content, blockIndentation + new string(' ', Math.Max(0,
                            width + shift - blockWidth)));
                    }
                }
                else if (!token.IsTrivia)
                {
                    depth += token.Text switch { "(" or "[" => 1, ")" or "]" => -1, _ => 0 };
                }
            }
        }

        var output = new List<SyntaxToken>();
        for (var index = 0; index < tokens.Count; index++)
        {
            if (replacements.TryGetValue(index, out var replacement))
            {
                var anchor = tokens[replacement.End];
                output.Add(new SyntaxToken(SyntaxKind.Whitespace, replacement.Indentation, anchor.Offset, anchor.Line, -1));
                index = replacement.End;
            }
            output.Add(tokens[index]);
        }
        return output;
    }

    private static int Width(IEnumerable<SyntaxToken> tokens, int tabWidth)
    {
        var width = 0;
        foreach (var character in tokens.SelectMany(token => token.Text))
        {
            width += character == '\t' ? tabWidth - width % tabWidth : 1;
        }
        return width;
    }

    internal sealed record Assignment(int Start, int Operator, int End, int StartLine, int EndLine);
}
