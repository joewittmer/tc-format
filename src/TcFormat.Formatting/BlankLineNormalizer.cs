using TcFormat.Core;
using TcFormat.Syntax;

namespace TcFormat.Formatting;

internal static class BlankLineNormalizer
{
    public static IReadOnlyList<SyntaxToken> Apply(
        IReadOnlyList<SyntaxToken> tokens,
        FormatterOptions options)
    {
        var lines = AnnotateContexts(SplitLines(tokens));
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
        if (line?.IsCaseLabel == true)
        {
            return options.AfterCaseLabel;
        }

        if (ContainsKeyword(line, "THEN"))
        {
            if (IsKeyword(line, "IF"))
            {
                return options.AfterIfThen;
            }

            if (IsKeyword(line, "ELSIF"))
            {
                return options.AfterElsifThen;
            }
        }

        return ContainsKeyword(line, "DO")
            ? options.AfterDo
            : BlankLinePolicy.Preserve;
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
            _ => BlankLinePolicy.Preserve
        };
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
        "UNTIL" or "END_REPEAT" => ControlFlowBlock.Repeat,
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

    private static bool ContainsKeyword(TokenLine? line, string keyword) =>
        line is not null && line.Tokens.Any(token =>
            token.Kind == SyntaxKind.Keyword &&
            string.Equals(token.Text, keyword, StringComparison.OrdinalIgnoreCase));

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
