using TcFormat.Core;
using TcFormat.Syntax;

namespace TcFormat.Formatting;

internal static class VerticalAligner
{
    public static IReadOnlyList<SyntaxToken> Apply(IReadOnlyList<SyntaxToken> tokens, FormatterOptions options)
    {
        var lines = SplitLines(tokens);

        AlignGroups(
            lines,
            IsDeclarationLine,
            FindAddressKeyword,
            options.Alignment.Addresses,
            options);
        AlignGroups(
            lines,
            IsDeclarationLine,
            FindDeclarationColon,
            options.Alignment.Declarations,
            options);
        AlignGroups(
            lines,
            IsDeclarationLine,
            FindDeclarationInitializer,
            options.Alignment.DeclarationInitializers,
            options);
        AlignGroups(
            lines,
            IsAssignmentLine,
            FindTopLevelAssignment,
            options.Alignment.Assignments,
            options);
        AlignNamedArgumentGroups(lines, options);
        AlignGroups(
            lines,
            IsCodeLine,
            FindEndOfLineComment,
            options.Alignment.EndOfLineComments,
            options);

        return JoinLines(lines);
    }

    private static void AlignNamedArgumentGroups(
        IReadOnlyList<TokenLine> lines,
        FormatterOptions options)
    {
        if (!options.Alignment.NamedInputs && !options.Alignment.NamedOutputs)
        {
            return;
        }

        var group = new List<TokenLine>();
        int? delimiterScope = null;
        int? argumentColumn = null;

        foreach (var line in lines)
        {
            var target = FindNamedArgument(line, options);
            if (target is null ||
                (group.Count > 0 &&
                 (delimiterScope != target.DelimiterScope ||
                  argumentColumn != target.ArgumentColumn)))
            {
                ApplyGroup(group, candidate => FindNamedArgument(candidate, options)?.Index ?? -1, options);
                group.Clear();
                delimiterScope = null;
                argumentColumn = null;
            }

            if (target is null)
            {
                continue;
            }

            delimiterScope ??= target.DelimiterScope;
            argumentColumn ??= target.ArgumentColumn;
            group.Add(line);
        }

        ApplyGroup(group, candidate => FindNamedArgument(candidate, options)?.Index ?? -1, options);
    }

    private static void AlignGroups(
        IReadOnlyList<TokenLine> lines,
        Func<TokenLine, bool> isCandidate,
        Func<TokenLine, int> findTarget,
        bool enabled,
        FormatterOptions options)
    {
        if (!enabled)
        {
            return;
        }

        var group = new List<TokenLine>();
        string? indentation = null;

        foreach (var line in lines)
        {
            var lineIndentation = GetLeadingWhitespace(line);
            if (!isCandidate(line) ||
                (group.Count > 0 && !string.Equals(indentation, lineIndentation, StringComparison.Ordinal)))
            {
                ApplyGroup(group, findTarget, options);
                group.Clear();
                indentation = null;
            }

            if (!isCandidate(line))
            {
                continue;
            }

            indentation ??= lineIndentation;
            group.Add(line);
        }

        ApplyGroup(group, findTarget, options);
    }

    private static void ApplyGroup(
        IReadOnlyList<TokenLine> group,
        Func<TokenLine, int> findTarget,
        FormatterOptions options)
    {
        var targets = group
            .Select(line => (Line: line, Index: findTarget(line)))
            .Where(target => target.Index >= 0)
            .ToArray();
        if (targets.Length < 2)
        {
            return;
        }

        var targetColumn = targets.Max(target => GetTokenColumn(target.Line, target.Index, options.Indentation.TabWidth));
        if (options.Layout.MaximumLineLength > 0 && targets.Any(target =>
                GetVisualWidth(target.Line.Tokens, options.Indentation.TabWidth) +
                targetColumn - GetTokenColumn(target.Line, target.Index, options.Indentation.TabWidth) >
                options.Layout.MaximumLineLength))
        {
            return;
        }

        foreach (var target in targets)
        {
            SetTokenColumn(target.Line, target.Index, targetColumn, options.Indentation.TabWidth);
        }
    }

    private static void SetTokenColumn(TokenLine line, int tokenIndex, int targetColumn, int tabWidth)
    {
        var whitespaceIndex = tokenIndex - 1;
        var hasWhitespace = whitespaceIndex >= 0 && line.Tokens[whitespaceIndex].Kind == SyntaxKind.Whitespace;
        var prefixEnd = hasWhitespace ? whitespaceIndex : tokenIndex;
        var prefixWidth = GetVisualWidth(line.Tokens.Take(prefixEnd), tabWidth);
        var spaces = Math.Max(0, targetColumn - prefixWidth);
        var anchor = line.Tokens[tokenIndex];

        if (hasWhitespace)
        {
            if (spaces == 0)
            {
                line.Tokens.RemoveAt(whitespaceIndex);
            }
            else
            {
                line.Tokens[whitespaceIndex] = CreateWhitespace(spaces, anchor);
            }

            return;
        }

        if (spaces > 0)
        {
            line.Tokens.Insert(tokenIndex, CreateWhitespace(spaces, anchor));
        }
    }

    private static bool IsDeclarationLine(TokenLine line) => FindDeclarationColon(line) >= 0;

    private static bool IsAssignmentLine(TokenLine line) =>
        FindDeclarationColon(line) < 0 && FindTopLevelAssignment(line) >= 0;

    private static bool IsCodeLine(TokenLine line) =>
        line.Tokens.Any(token => token.Kind is not
            SyntaxKind.Whitespace and not
            SyntaxKind.LineComment and not
            SyntaxKind.BlockComment) &&
        !ContainsMultilineToken(line);

    private static int FindDeclarationColon(TokenLine line)
    {
        var significant = SignificantIndices(line).ToArray();
        for (var position = 0; position < significant.Length; position++)
        {
            var index = significant[position];
            if (line.Tokens[index].Text == ":" &&
                GetDelimiterDepth(line, index) == 0 &&
                position < significant.Length - 1)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindDeclarationInitializer(TokenLine line)
    {
        if (FindDeclarationColon(line) < 0)
        {
            return -1;
        }

        return FindTokenAtTopLevel(line, ":=");
    }

    private static int FindTopLevelAssignment(TokenLine line) => FindTokenAtTopLevel(line, ":=");

    private static NamedArgumentTarget? FindNamedArgument(TokenLine line, FormatterOptions options)
    {
        var scopes = new List<DelimiterScope>(line.DelimiterScopesBefore);
        for (var index = 0; index < line.Tokens.Count; index++)
        {
            var token = line.Tokens[index];
            if (token.Kind is SyntaxKind.LineComment or SyntaxKind.BlockComment)
            {
                continue;
            }

            if (token.Text is "(" or "[")
            {
                scopes.Add(new DelimiterScope(token.Text, token.Offset));
                continue;
            }

            if (token.Text is ")" or "]")
            {
                if (scopes.Count > 0)
                {
                    scopes.RemoveAt(scopes.Count - 1);
                }

                continue;
            }

            var enabled = token.Text switch
            {
                ":=" => options.Alignment.NamedInputs,
                "=>" => options.Alignment.NamedOutputs,
                _ => false
            };
            if (enabled && scopes.LastOrDefault()?.OpeningToken == "(")
            {
                var argumentIndex = index - 1;
                while (argumentIndex >= 0 && line.Tokens[argumentIndex].Kind == SyntaxKind.Whitespace)
                {
                    argumentIndex--;
                }

                return new NamedArgumentTarget(
                    index,
                    scopes[^1].Offset,
                    argumentIndex >= 0
                        ? GetTokenColumn(line, argumentIndex, options.Indentation.TabWidth)
                        : 0);
            }
        }

        return null;
    }

    private static int FindAddressKeyword(TokenLine line)
    {
        if (FindDeclarationColon(line) < 0)
        {
            return -1;
        }

        for (var index = 0; index < line.Tokens.Count; index++)
        {
            var token = line.Tokens[index];
            if (token.Kind == SyntaxKind.Keyword &&
                string.Equals(token.Text, "AT", StringComparison.OrdinalIgnoreCase) &&
                GetDelimiterDepth(line, index) == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindEndOfLineComment(TokenLine line)
    {
        for (var index = 0; index < line.Tokens.Count; index++)
        {
            if (line.Tokens[index].Kind == SyntaxKind.LineComment)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindTokenAtTopLevel(TokenLine line, string text)
    {
        for (var index = 0; index < line.Tokens.Count; index++)
        {
            if (line.Tokens[index].Text == text && GetDelimiterDepth(line, index) == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int GetDelimiterDepth(TokenLine line, int exclusiveEnd)
    {
        var depth = line.DelimiterDepthBefore;
        for (var index = 0; index < exclusiveEnd; index++)
        {
            if (line.Tokens[index].Kind is SyntaxKind.LineComment or SyntaxKind.BlockComment)
            {
                continue;
            }

            depth += line.Tokens[index].Text switch
            {
                "(" or "[" => 1,
                ")" or "]" => -1,
                _ => 0
            };
        }

        return depth;
    }

    private static IEnumerable<int> SignificantIndices(TokenLine line)
    {
        for (var index = 0; index < line.Tokens.Count; index++)
        {
            if (line.Tokens[index].Kind is not
                SyntaxKind.Whitespace and not
                SyntaxKind.LineComment and not
                SyntaxKind.BlockComment)
            {
                yield return index;
            }
        }
    }

    private static string GetLeadingWhitespace(TokenLine line) =>
        line.Tokens.Count > 0 && line.Tokens[0].Kind == SyntaxKind.Whitespace
            ? line.Tokens[0].Text
            : string.Empty;

    private static int GetTokenColumn(TokenLine line, int tokenIndex, int tabWidth) =>
        GetVisualWidth(line.Tokens.Take(tokenIndex), tabWidth);

    private static int GetVisualWidth(IEnumerable<SyntaxToken> tokens, int tabWidth)
    {
        var width = 0;
        foreach (var character in tokens.SelectMany(token => token.Text))
        {
            width = character == '\t'
                ? width + tabWidth - width % tabWidth
                : width + 1;
        }

        return width;
    }

    private static bool ContainsMultilineToken(TokenLine line) =>
        line.Tokens.Any(token => token.Text.AsSpan().IndexOfAny('\r', '\n') >= 0);

    private static IReadOnlyList<TokenLine> SplitLines(IReadOnlyList<SyntaxToken> tokens)
    {
        var lines = new List<TokenLine>();
        var delimiterScopes = new List<DelimiterScope>();
        var current = new TokenLine([], null, []);

        foreach (var token in tokens)
        {
            if (token.Kind == SyntaxKind.NewLine)
            {
                current.NewLine = token;
                lines.Add(current);
                current = new TokenLine([], null, [.. delimiterScopes]);
                continue;
            }

            current.Tokens.Add(token);
            if (token.Kind is not SyntaxKind.LineComment and not SyntaxKind.BlockComment)
            {
                if (token.Text is "(" or "[")
                {
                    delimiterScopes.Add(new DelimiterScope(token.Text, token.Offset));
                }
                else if (token.Text is ")" or "]" && delimiterScopes.Count > 0)
                {
                    delimiterScopes.RemoveAt(delimiterScopes.Count - 1);
                }
            }
        }

        if (current.Tokens.Count > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    private static IReadOnlyList<SyntaxToken> JoinLines(IEnumerable<TokenLine> lines)
    {
        var tokens = new List<SyntaxToken>();
        foreach (var line in lines)
        {
            tokens.AddRange(line.Tokens);
            if (line.NewLine is not null)
            {
                tokens.Add(line.NewLine);
            }
        }

        return tokens;
    }

    private static SyntaxToken CreateWhitespace(int spaces, SyntaxToken anchor) =>
        new(SyntaxKind.Whitespace, new string(' ', spaces), anchor.Offset, anchor.Line, anchor.Column);

    private sealed class TokenLine(
        List<SyntaxToken> tokens,
        SyntaxToken? newLine,
        IReadOnlyList<DelimiterScope> delimiterScopesBefore)
    {
        public List<SyntaxToken> Tokens { get; } = tokens;

        public SyntaxToken? NewLine { get; set; } = newLine;

        public IReadOnlyList<DelimiterScope> DelimiterScopesBefore { get; } = delimiterScopesBefore;

        public int DelimiterDepthBefore => DelimiterScopesBefore.Count;
    }

    private sealed record DelimiterScope(string OpeningToken, int Offset);

    private sealed record NamedArgumentTarget(int Index, int DelimiterScope, int ArgumentColumn);
}

