using TcFormat.Core;
using TcFormat.Syntax;

namespace TcFormat.Formatting;

internal static class BlankLineNormalizer
{
    public static IReadOnlyList<SyntaxToken> Apply(
        IReadOnlyList<SyntaxToken> tokens,
        FormatterOptions options)
    {
        var lines = AnnotateControlHeaders(AnnotateContexts(SplitLines(tokens)));
        if (options.BlankLines.AfterMultilineCall != BlankLinePolicy.Preserve)
        {
            lines = AnnotateMultilineCalls(lines);
        }

        var withoutForbiddenBlankLines = RemoveForbiddenBlankLines(lines, options.BlankLines);
        var normalized = AddRequiredBlankLines(withoutForbiddenBlankLines, options.BlankLines);

        return normalized
            .SelectMany(line => line.NewLine is null ? line.Tokens : [.. line.Tokens, line.NewLine])
            .ToArray();
    }

    private static IReadOnlyList<TokenLine> SplitLines(IReadOnlyList<SyntaxToken> tokens)
    {
        var lines = new List<TokenLine>();
        var lineTokens = new List<SyntaxToken>();

        foreach (var token in tokens)
        {
            if (token.Kind != SyntaxKind.NewLine)
            {
                lineTokens.Add(token);
                continue;
            }

            lines.Add(new TokenLine([.. lineTokens], token));
            lineTokens.Clear();
        }

        if (lineTokens.Count > 0)
        {
            lines.Add(new TokenLine([.. lineTokens], null));
        }

        return lines;
    }

    private static IReadOnlyList<TokenLine> RemoveForbiddenBlankLines(
        IReadOnlyList<TokenLine> lines,
        BlankLineOptions options)
    {
        var output = new List<TokenLine>(lines.Count);

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (!line.IsBlank)
            {
                output.Add(line);
                continue;
            }

            var previous = FindPreviousContentLine(lines, index);
            var next = FindNextContentLine(lines, index);
            if (GetBoundaryPolicy(previous, next, options) == BlankLinePolicy.Remove)
            {
                continue;
            }

            output.Add(line);
        }

        return output;
    }

    private static IReadOnlyList<TokenLine> AddRequiredBlankLines(
        IReadOnlyList<TokenLine> lines,
        BlankLineOptions options)
    {
        var output = new List<TokenLine>(lines.Count);

        foreach (var line in lines)
        {
            var previous = FindPreviousContentLine(output, output.Count);
            if (previous is not null &&
                GetBoundaryPolicy(previous, line, options) == BlankLinePolicy.Require)
            {
                while (output.Count > 0 && output[^1].IsBlank)
                {
                    output.RemoveAt(output.Count - 1);
                }

                output.Add(CreateBlankLine(output, line));
            }

            output.Add(line);
        }

        return output;
    }

    private static TokenLine CreateBlankLine(IReadOnlyList<TokenLine> output, TokenLine next)
    {
        var newLine = output
            .Select(line => line.NewLine)
            .LastOrDefault(token => token is not null) ?? next.NewLine;

        if (newLine is null)
        {
            throw new InvalidOperationException("A blank line cannot be inserted without a line ending.");
        }

        return new TokenLine([], newLine);
    }

    private static bool RemovesFollowingBlankLines(TokenLine? line)
    {
        var keyword = FirstKeyword(line);
        return IsVariableKeyword(keyword) ||
               string.Equals(keyword, "METHOD", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(keyword, "ELSE", StringComparison.OrdinalIgnoreCase);
    }

    private static BlankLinePolicy GetBoundaryPolicy(
        TokenLine? previous,
        TokenLine? next,
        BlankLineOptions options)
    {
        if (RemovesFollowingBlankLines(previous))
        {
            return BlankLinePolicy.Remove;
        }

        var after = GetFollowingBlankLinePolicy(previous, options);
        var before = GetPrecedingBlankLinePolicy(next, options);
        if (after == BlankLinePolicy.Remove || before == BlankLinePolicy.Remove)
        {
            return BlankLinePolicy.Remove;
        }

        if (after == BlankLinePolicy.Require || before == BlankLinePolicy.Require)
        {
            return BlankLinePolicy.Require;
        }

        return BlankLinePolicy.Preserve;
    }

    private static BlankLinePolicy GetFollowingBlankLinePolicy(
        TokenLine? line,
        BlankLineOptions options)
    {
        if (line?.EndsMultilineCall == true)
        {
            return options.AfterMultilineCall;
        }

        if (line?.IsCaseLabel == true)
        {
            return options.AfterCaseLabel;
        }

        if (IsKeyword(line, "REPEAT"))
        {
            return options.AfterRepeat;
        }

        if (FirstKeyword(line)?.ToUpperInvariant() is "END_IF" or "END_CASE" or "END_FOR" or "END_WHILE" or "END_REPEAT")
        {
            return options.AfterControlFlowBlock;
        }

        if (line?.CompletedHeader is not null)
        {
            var policy = line.CompletedHeader switch
            {
                "IF" => options.AfterIfThen,
                "ELSIF" => options.AfterElsifThen,
                _ => options.AfterDo
            };
            return policy == BlankLinePolicy.Multiline
                ? line.HasMultilineHeader ? BlankLinePolicy.Require : BlankLinePolicy.Remove
                : policy;
        }

        return BlankLinePolicy.Preserve;
    }

    private static BlankLinePolicy GetPrecedingBlankLinePolicy(
        TokenLine? line,
        BlankLineOptions options)
    {
        if (line?.IsCaseLabel == true)
        {
            return options.BeforeCaseLabel;
        }

        var keyword = FirstKeyword(line);
        if (IsVariableKeyword(keyword))
        {
            return options.BeforeVariableBlock;
        }

        return keyword?.ToUpperInvariant() switch
        {
            "IF" => options.BeforeIf,
            "CASE" => options.BeforeCase,
            "ELSE" => line?.ElseContext == ControlFlowBlock.Case
                ? options.BeforeCaseElse
                : options.BeforeIfElse,
            "ELSIF" => options.BeforeElsif,
            "END_VAR" => options.BeforeEndVar,
            "END_IF" => options.BeforeEndIf,
            "END_CASE" => options.BeforeEndCase,
            "FOR" or "WHILE" or "REPEAT" => options.BeforeLoop,
            "UNTIL" => options.BeforeUntil,
            "END_FOR" or "END_WHILE" or "END_REPEAT" => options.BeforeEndLoop,
            _ => BlankLinePolicy.Preserve
        };
    }

    private static IReadOnlyList<TokenLine> AnnotateControlHeaders(IReadOnlyList<TokenLine> lines)
    {
        var output = lines.ToArray();
        string? header = null;
        var startLine = 0;
        var depth = 0;

        for (var lineIndex = 0; lineIndex < output.Length; lineIndex++)
        {
            var significant = output[lineIndex].Tokens
                .Where(token => token.Kind is not SyntaxKind.Whitespace and not SyntaxKind.LineComment and not SyntaxKind.BlockComment)
                .ToArray();
            if (depth == 0 && significant.Length > 0)
            {
                var first = significant[0];
                if (first.Kind == SyntaxKind.Keyword && first.Text.ToUpperInvariant() is "IF" or "ELSIF" or "FOR" or "WHILE")
                {
                    header = first.Text.ToUpperInvariant();
                    startLine = lineIndex;
                }
            }

            for (var index = 0; index < significant.Length; index++)
            {
                var token = significant[index];
                depth += token.Text switch { "(" or "[" => 1, ")" or "]" => -1, _ => 0 };
                var terminator = header is "IF" or "ELSIF" ? "THEN" : "DO";
                if (header is not null && depth == 0 && token.Kind == SyntaxKind.Keyword &&
                    string.Equals(token.Text, terminator, StringComparison.OrdinalIgnoreCase))
                {
                    if (index == significant.Length - 1)
                    {
                        output[lineIndex] = output[lineIndex] with
                        {
                            CompletedHeader = header,
                            HasMultilineHeader = lineIndex > startLine
                        };
                    }

                    header = null;
                }
                else if (depth == 0 && token.Text == ";")
                {
                    header = null;
                }
            }
        }

        return output;
    }

    private static IReadOnlyList<TokenLine> AnnotateMultilineCalls(IReadOnlyList<TokenLine> lines)
    {
        var output = lines.ToArray();
        List<SyntaxToken>? statement = null;
        var startLine = 0;
        var depth = 0;

        for (var lineIndex = 0; lineIndex < output.Length; lineIndex++)
        {
            var significant = output[lineIndex].Tokens
                .Where(token => token.Kind is not SyntaxKind.Whitespace and not SyntaxKind.LineComment and not SyntaxKind.BlockComment)
                .ToArray();
            if (statement is null && depth == 0 && significant.Length > 0 &&
                (significant[0].Kind == SyntaxKind.Identifier ||
                 significant[0].Text.ToUpperInvariant() is "THIS" or "SUPER"))
            {
                statement = [];
                startLine = lineIndex;
            }

            for (var index = 0; index < significant.Length; index++)
            {
                var token = significant[index];
                statement?.Add(token);
                depth += token.Text switch
                {
                    "(" or "[" => 1,
                    ")" or "]" => -1,
                    _ => 0
                };

                if (depth == 0 && token.Text == ";")
                {
                    if (statement is not null && lineIndex > startLine &&
                        index == significant.Length - 1 && IsStandaloneCall(statement))
                    {
                        output[lineIndex] = output[lineIndex] with { EndsMultilineCall = true };
                    }

                    statement = null;
                }
            }
        }

        return output;
    }

    private static bool IsStandaloneCall(IReadOnlyList<SyntaxToken> tokens)
    {
        var index = 1;
        while (index < tokens.Count)
        {
            if (tokens[index].Text == "." && index + 1 < tokens.Count &&
                tokens[index + 1].Kind == SyntaxKind.Identifier)
            {
                index += 2;
            }
            else if (tokens[index].Text == "^")
            {
                index++;
            }
            else if (tokens[index].Text == "[")
            {
                var depth = 1;
                while (++index < tokens.Count && depth > 0)
                {
                    depth += tokens[index].Text switch { "[" => 1, "]" => -1, _ => 0 };
                }
            }
            else
            {
                break;
            }
        }

        if (index >= tokens.Count || tokens[index].Text != "(")
        {
            return false;
        }

        var parentheses = 1;
        while (++index < tokens.Count)
        {
            parentheses += tokens[index].Text switch { "(" => 1, ")" => -1, _ => 0 };
            if (parentheses == 0)
            {
                return index == tokens.Count - 2 && tokens[index + 1].Text == ";";
            }
        }

        return false;
    }

    private static IReadOnlyList<TokenLine> AnnotateContexts(IReadOnlyList<TokenLine> lines)
    {
        var output = lines.ToArray();
        var blocks = new List<ControlFlowBlock>();

        for (var index = 0; index < output.Length; index++)
        {
            var significant = output[index].Tokens
                .Where(token => token.Kind is not SyntaxKind.Whitespace and not SyntaxKind.LineComment and not SyntaxKind.BlockComment)
                .ToArray();
            var keyword = FirstKeyword(output[index]);

            if (significant.Length > 0 &&
                significant[^1].Text == ":" &&
                blocks.LastOrDefault() is ControlFlowBlock.Case or ControlFlowBlock.CaseBranch)
            {
                output[index] = output[index] with { IsCaseLabel = true };
                RemoveTrailingCaseBranch(blocks);
                blocks.Add(ControlFlowBlock.CaseBranch);
                continue;
            }

            if (string.Equals(keyword, "ELSE", StringComparison.OrdinalIgnoreCase))
            {
                var isCaseElse = blocks.LastOrDefault() is ControlFlowBlock.Case or ControlFlowBlock.CaseBranch;
                output[index] = output[index] with
                {
                    ElseContext = isCaseElse ? ControlFlowBlock.Case : ControlFlowBlock.If
                };

                if (isCaseElse)
                {
                    RemoveTrailingCaseBranch(blocks);
                    blocks.Add(ControlFlowBlock.CaseBranch);
                }

                continue;
            }

            var closeBlock = GetCloseBlock(keyword);
            if (closeBlock is not null)
            {
                if (closeBlock == ControlFlowBlock.Case)
                {
                    RemoveTrailingCaseBranch(blocks);
                }

                RemoveLast(blocks, closeBlock.Value);
                continue;
            }

            var openBlock = GetOpenBlock(keyword);
            if (openBlock is not null)
            {
                blocks.Add(openBlock.Value);
            }
        }

        return output;
    }

    private static ControlFlowBlock? GetOpenBlock(string? keyword) => keyword?.ToUpperInvariant() switch
    {
        "IF" => ControlFlowBlock.If,
        "CASE" => ControlFlowBlock.Case,
        "FOR" => ControlFlowBlock.For,
        "WHILE" => ControlFlowBlock.While,
        "REPEAT" => ControlFlowBlock.Repeat,
        "STRUCT" => ControlFlowBlock.Struct,
        "UNION" => ControlFlowBlock.Union,
        "__TRY" => ControlFlowBlock.Try,
        "VAR" or "VAR_ACCESS" or "VAR_CONFIG" or "VAR_EXTERNAL" or "VAR_GLOBAL" or
            "VAR_IN_OUT" or "VAR_INPUT" or "VAR_INST" or "VAR_OUTPUT" or "VAR_STAT" or
            "VAR_TEMP" => ControlFlowBlock.Var,
        _ => null
    };

    private static ControlFlowBlock? GetCloseBlock(string? keyword) => keyword?.ToUpperInvariant() switch
    {
        "END_IF" => ControlFlowBlock.If,
        "END_CASE" => ControlFlowBlock.Case,
        "END_FOR" => ControlFlowBlock.For,
        "END_WHILE" => ControlFlowBlock.While,
        "END_REPEAT" => ControlFlowBlock.Repeat,
        "END_STRUCT" => ControlFlowBlock.Struct,
        "END_UNION" => ControlFlowBlock.Union,
        "END_VAR" => ControlFlowBlock.Var,
        "__ENDTRY" => ControlFlowBlock.Try,
        _ => null
    };

    private static void RemoveTrailingCaseBranch(IList<ControlFlowBlock> blocks)
    {
        if (blocks.Count > 0 && blocks[^1] == ControlFlowBlock.CaseBranch)
        {
            blocks.RemoveAt(blocks.Count - 1);
        }
    }

    private static void RemoveLast(IList<ControlFlowBlock> blocks, ControlFlowBlock block)
    {
        for (var index = blocks.Count - 1; index >= 0; index--)
        {
            if (blocks[index] == block)
            {
                blocks.RemoveAt(index);
                return;
            }
        }
    }

    private static bool IsVariableKeyword(string? keyword) =>
        keyword is not null &&
        (string.Equals(keyword, "VAR", StringComparison.OrdinalIgnoreCase) ||
         keyword.StartsWith("VAR_", StringComparison.OrdinalIgnoreCase));

    private static bool IsKeyword(TokenLine? line, string keyword) =>
        string.Equals(FirstKeyword(line), keyword, StringComparison.OrdinalIgnoreCase);

    private static string? FirstKeyword(TokenLine? line)
    {
        if (line is null)
        {
            return null;
        }

        foreach (var token in line.Tokens)
        {
            if (token.Kind == SyntaxKind.Whitespace)
            {
                continue;
            }

            return token.Kind == SyntaxKind.Keyword ? token.Text : null;
        }

        return null;
    }

    private static TokenLine? FindPreviousContentLine(IReadOnlyList<TokenLine> lines, int index)
    {
        for (var candidate = index - 1; candidate >= 0; candidate--)
        {
            if (!lines[candidate].IsBlank)
            {
                return lines[candidate];
            }
        }

        return null;
    }

    private static TokenLine? FindNextContentLine(IReadOnlyList<TokenLine> lines, int index)
    {
        for (var candidate = index + 1; candidate < lines.Count; candidate++)
        {
            if (!lines[candidate].IsBlank)
            {
                return lines[candidate];
            }
        }

        return null;
    }

    private sealed record TokenLine(IReadOnlyList<SyntaxToken> Tokens, SyntaxToken? NewLine)
    {
        public bool IsBlank => Tokens.All(token => token.Kind == SyntaxKind.Whitespace);

        public ControlFlowBlock ElseContext { get; init; }

        public bool IsCaseLabel { get; init; }

        public bool EndsMultilineCall { get; init; }

        public string? CompletedHeader { get; init; }

        public bool HasMultilineHeader { get; init; }
    }

    private enum ControlFlowBlock
    {
        If,
        Case,
        CaseBranch,
        For,
        While,
        Repeat,
        Var,
        Struct,
        Union,
        Try
    }
}
