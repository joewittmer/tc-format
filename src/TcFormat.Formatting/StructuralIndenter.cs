using TcFormat.Core;
using TcFormat.Syntax;

namespace TcFormat.Formatting;

internal static class StructuralIndenter
{
    public static StructuralIndentResult Apply(IReadOnlyList<SyntaxToken> tokens, FormatterOptions options)
    {
        var state = new IndentationState(options);
        var output = new List<SyntaxToken>(tokens.Count);
        var line = new List<SyntaxToken>();

        foreach (var token in tokens)
        {
            if (token.Kind != SyntaxKind.NewLine)
            {
                line.Add(token);
                continue;
            }

            state.FormatLine(line, output);
            output.Add(token);
            line.Clear();
        }

        state.FormatLine(line, output);
        state.Complete();
        return new StructuralIndentResult(output, state.Diagnostics);
    }

    private sealed class IndentationState(FormatterOptions options)
    {
        private readonly List<Block> blocks = [];
        private int continuationDepth;

        public List<FormatDiagnostic> Diagnostics { get; } = [];

        public void FormatLine(IReadOnlyList<SyntaxToken> line, ICollection<SyntaxToken> output)
        {
            var significant = line.Where(IsSignificant).ToArray();
            if (significant.Length == 0)
            {
                foreach (var token in line)
                {
                    output.Add(token);
                }

                return;
            }

            var containsMultilineToken = line.Any(token =>
                token.Kind != SyntaxKind.NewLine &&
                token.Text.AsSpan().IndexOfAny('\r', '\n') >= 0);
            var isCaseLabel = IsCaseLabel(significant);
            var displayDepth = ComputeDisplayDepth(significant[0], isCaseLabel);
            var startsWithClosingDelimiter = significant[0].Text is ")" or "]";
            var continuation = continuationDepth > 0 && !startsWithClosingDelimiter;

            if (containsMultilineToken)
            {
                foreach (var token in line)
                {
                    output.Add(token);
                }
            }
            else
            {
                AddWithIndentation(line, output, displayDepth, continuation);
            }

            UpdateBlocks(significant, isCaseLabel);
            UpdateContinuationDepth(significant);
        }

        public void Complete()
        {
            foreach (var block in blocks.Where(block => block.Kind != BlockKind.CaseBranch))
            {
                Diagnostics.Add(new FormatDiagnostic(
                    $"Unclosed {GetDisplayName(block.Kind)} block opened on line {block.Line}.",
                    block.Line,
                    1));
            }

            if (continuationDepth > 0)
            {
                Diagnostics.Add(new FormatDiagnostic("Unclosed parenthesis or bracket."));
            }
        }

        private int ComputeDisplayDepth(SyntaxToken first, bool isCaseLabel)
        {
            var depth = blocks.Count;
            if (TryGetCloseKind(first, out var closeKind))
            {
                depth -= closeKind == BlockKind.Case && blocks.LastOrDefault()?.Kind == BlockKind.CaseBranch
                    ? 2
                    : 1;
            }
            else if (IsBranch(first))
            {
                depth--;
            }
            else if (isCaseLabel)
            {
                if (blocks.LastOrDefault()?.Kind == BlockKind.CaseBranch)
                {
                    depth--;
                }

                if (!options.Indentation.IndentCaseLabels)
                {
                    depth--;
                }
            }

            return Math.Max(0, depth);
        }

        private void AddWithIndentation(
            IReadOnlyList<SyntaxToken> line,
            ICollection<SyntaxToken> output,
            int displayDepth,
            bool continuation)
        {
            var firstContent = 0;
            while (firstContent < line.Count && line[firstContent].Kind == SyntaxKind.Whitespace)
            {
                firstContent++;
            }

            var indentation = CreateIndentation(displayDepth, continuation);
            if (indentation.Length > 0)
            {
                var first = line[firstContent];
                output.Add(new SyntaxToken(
                    SyntaxKind.Whitespace,
                    indentation,
                    first.Offset,
                    first.Line,
                    1));
            }

            for (var index = firstContent; index < line.Count; index++)
            {
                output.Add(line[index]);
            }
        }

        private string CreateIndentation(int displayDepth, bool continuation)
        {
            if (options.Indentation.Style == IndentStyle.Spaces)
            {
                var width = displayDepth * options.Indentation.Size;
                if (continuation)
                {
                    width += options.Indentation.ContinuationSize;
                }

                return new string(' ', width);
            }

            var indent = new string('\t', displayDepth);
            if (!continuation)
            {
                return indent;
            }

            return options.Indentation.ContinuationSize == options.Indentation.TabWidth
                ? indent + '\t'
                : indent + new string(' ', options.Indentation.ContinuationSize);
        }

        private void UpdateBlocks(IReadOnlyList<SyntaxToken> significant, bool isCaseLabel)
        {
            if (isCaseLabel)
            {
                PopCaseBranch();
                blocks.Add(new Block(BlockKind.CaseBranch, significant[0].Line));
                return;
            }

            for (var index = 0; index < significant.Count; index++)
            {
                var token = significant[index];
                if (TryGetCloseKind(token, out var closeKind))
                {
                    PopExpected(closeKind, token);
                    continue;
                }

                if (IsCaseElse(token))
                {
                    PopCaseBranch();
                    blocks.Add(new Block(BlockKind.CaseBranch, token.Line));
                    continue;
                }

                if (TryGetOpenKind(token, out var openKind))
                {
                    blocks.Add(new Block(openKind, token.Line));
                }
            }
        }

        private bool IsCaseElse(SyntaxToken token) =>
            IsKeyword(token, "ELSE") && blocks.LastOrDefault()?.Kind is BlockKind.Case or BlockKind.CaseBranch;

        private void PopExpected(BlockKind expected, SyntaxToken token)
        {
            if (expected == BlockKind.Case)
            {
                PopCaseBranch();
            }

            if (blocks.Count == 0 || blocks[^1].Kind != expected)
            {
                Diagnostics.Add(new FormatDiagnostic(
                    $"Unexpected {token.Text}; expected the end of " +
                    $"{(blocks.Count == 0 ? "no open block" : GetDisplayName(blocks[^1].Kind))}.",
                    token.Line,
                    token.Column));
                return;
            }

            blocks.RemoveAt(blocks.Count - 1);
        }

        private void PopCaseBranch()
        {
            if (blocks.LastOrDefault()?.Kind == BlockKind.CaseBranch)
            {
                blocks.RemoveAt(blocks.Count - 1);
            }
        }

        private void UpdateContinuationDepth(IEnumerable<SyntaxToken> significant)
        {
            foreach (var token in significant)
            {
                continuationDepth += token.Text switch
                {
                    "(" or "[" => 1,
                    ")" or "]" => -1,
                    _ => 0
                };

                if (continuationDepth < 0)
                {
                    Diagnostics.Add(new FormatDiagnostic(
                        $"Unexpected closing delimiter {token.Text}.",
                        token.Line,
                        token.Column));
                    continuationDepth = 0;
                }
            }
        }

        private bool IsCaseLabel(IReadOnlyList<SyntaxToken> significant) =>
            significant[^1].Text == ":" &&
            blocks.LastOrDefault()?.Kind is BlockKind.Case or BlockKind.CaseBranch;

        private bool IsBranch(SyntaxToken token)
        {
            if (IsKeyword(token, "ELSIF") || IsKeyword(token, "__CATCH") || IsKeyword(token, "__FINALLY"))
            {
                return true;
            }

            return IsKeyword(token, "ELSE");
        }

        private static bool TryGetOpenKind(SyntaxToken token, out BlockKind kind)
        {
            kind = token.Text.ToUpperInvariant() switch
            {
                "IF" => BlockKind.If,
                "CASE" => BlockKind.Case,
                "FOR" => BlockKind.For,
                "WHILE" => BlockKind.While,
                "REPEAT" => BlockKind.Repeat,
                "STRUCT" => BlockKind.Struct,
                "UNION" => BlockKind.Union,
                "__TRY" => BlockKind.Try,
                "VAR" or "VAR_ACCESS" or "VAR_CONFIG" or "VAR_EXTERNAL" or "VAR_GLOBAL" or
                    "VAR_IN_OUT" or "VAR_INPUT" or "VAR_INST" or "VAR_OUTPUT" or "VAR_STAT" or
                    "VAR_TEMP" => BlockKind.Var,
                _ => BlockKind.None
            };
            return kind != BlockKind.None && token.Kind == SyntaxKind.Keyword;
        }

        private static bool TryGetCloseKind(SyntaxToken token, out BlockKind kind)
        {
            kind = token.Text.ToUpperInvariant() switch
            {
                "END_IF" => BlockKind.If,
                "END_CASE" => BlockKind.Case,
                "END_FOR" => BlockKind.For,
                "END_WHILE" => BlockKind.While,
                "UNTIL" or "END_REPEAT" => BlockKind.Repeat,
                "END_STRUCT" => BlockKind.Struct,
                "END_UNION" => BlockKind.Union,
                "END_VAR" => BlockKind.Var,
                "__ENDTRY" => BlockKind.Try,
                _ => BlockKind.None
            };
            return kind != BlockKind.None && token.Kind == SyntaxKind.Keyword;
        }

        private static bool IsSignificant(SyntaxToken token) => token.Kind is not
            SyntaxKind.Whitespace and not
            SyntaxKind.LineComment and not
            SyntaxKind.BlockComment;

        private static bool IsKeyword(SyntaxToken token, string keyword) =>
            token.Kind == SyntaxKind.Keyword && string.Equals(token.Text, keyword, StringComparison.OrdinalIgnoreCase);

        private static string GetDisplayName(BlockKind kind) => kind switch
        {
            BlockKind.If => "IF",
            BlockKind.Case => "CASE",
            BlockKind.CaseBranch => "CASE branch",
            BlockKind.For => "FOR",
            BlockKind.While => "WHILE",
            BlockKind.Repeat => "REPEAT",
            BlockKind.Var => "VAR",
            BlockKind.Struct => "STRUCT",
            BlockKind.Union => "UNION",
            BlockKind.Try => "__TRY",
            _ => kind.ToString()
        };

        private sealed record Block(BlockKind Kind, int Line);
    }

    private enum BlockKind
    {
        None,
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

internal sealed record StructuralIndentResult(
    IReadOnlyList<SyntaxToken> Tokens,
    IReadOnlyList<FormatDiagnostic> Diagnostics);

