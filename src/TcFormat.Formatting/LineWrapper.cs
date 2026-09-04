using TcFormat.Core;
using TcFormat.Syntax;

namespace TcFormat.Formatting;

internal static class LineWrapper
{
    public static IReadOnlyList<SyntaxToken> Apply(
        IReadOnlyList<SyntaxToken> tokens,
        FormatterOptions options)
    {
        if (AllWrappingIsPreserved(options))
        {
            return tokens;
        }

        var output = tokens.ToList();
        var maximumBreaks = tokens.Count;
        for (var attempt = 0; attempt < maximumBreaks; attempt++)
        {
            var analysis = Analyze(output, options);
            var candidate = SelectCandidate(analysis, options.Layout.MaximumLineLength);
            if (candidate is null)
            {
                return output;
            }

            ApplyBreak(output, candidate);
        }

        return output;
    }

    private static LayoutAnalysis Analyze(IReadOnlyList<SyntaxToken> tokens, FormatterOptions options)
    {
        var lines = CreateLines(tokens, options.Indentation.TabWidth);
        var lineByToken = new LineInfo?[tokens.Count];
        foreach (var line in lines)
        {
            for (var index = line.Start; index < line.End; index++)
            {
                lineByToken[index] = line;
            }
        }

        var candidates = new List<BreakCandidate>();
        var scopes = new List<DelimiterScope>();
        SyntaxToken? previousCode = null;

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Kind == SyntaxKind.NewLine)
            {
                continue;
            }

            if (token.Kind is SyntaxKind.Whitespace or SyntaxKind.LineComment or SyntaxKind.BlockComment)
            {
                continue;
            }

            var line = lineByToken[index]!;
            if (token.Text is "(" or "[")
            {
                var scope = CreateScope(tokens, index, previousCode, line, scopes, options);
                scopes.Add(scope);
                var openingStyle = scope.UseHangingIndentation
                    ? WrapStyle.Preserve
                    : ShouldBreakAfterOpeningDelimiter(tokens, index, line, scope, options)
                        ? WrapStyle.Always
                        : scope.Style;
                AddAfterCandidate(
                    tokens,
                    index,
                    line,
                    openingStyle,
                    scopes[0].BaseIndentation,
                    candidates,
                    options);
                previousCode = token;
                continue;
            }

            if (token.Text is ")" or "]")
            {
                if (scopes.Count > 0)
                {
                    var scope = scopes[^1];
                    if (scope.Style == WrapStyle.Always)
                    {
                        AddBeforeCandidate(tokens, index, line, scope.Style, scope.BaseIndentation, candidates);
                    }

                    scopes.RemoveAt(scopes.Count - 1);
                }

                previousCode = token;
                continue;
            }

            if (token.Text == "," && scopes.Count > 0)
            {
                var scope = scopes[^1];
                if (scope.UseHangingIndentation)
                {
                    AddHangingAfterCandidate(
                        tokens,
                        index,
                        line,
                        scope.HangingIndentation,
                        candidates);
                }
                else
                {
                    AddAfterCandidate(
                        tokens,
                        index,
                        line,
                        scope.Style,
                        scopes[0].BaseIndentation,
                        candidates,
                        options);
                }
            }

            if (IsBinaryOperator(tokens, index))
            {
                AddBinaryCandidate(tokens, index, line, scopes, candidates, options);
            }

            previousCode = token;
        }

        return new LayoutAnalysis(lines, candidates);
    }

    private static BreakCandidate? SelectCandidate(LayoutAnalysis analysis, int maximumLineLength)
    {
        var forced = analysis.Candidates.FirstOrDefault(candidate => candidate.Style == WrapStyle.Always);
        if (forced is not null)
        {
            return forced;
        }

        if (maximumLineLength == 0)
        {
            return null;
        }

        foreach (var line in analysis.Lines.Where(line => !line.ContainsMultilineToken && line.Width > maximumLineLength))
        {
            var candidates = analysis.Candidates
                .Where(candidate => candidate.Style == WrapStyle.WhenLong && candidate.Line == line)
                .ToArray();
            if (candidates.Length == 0)
            {
                continue;
            }

            return candidates
                       .Where(candidate => candidate.Column <= maximumLineLength)
                       .MaxBy(candidate => candidate.Column)
                   ?? candidates.MinBy(candidate => candidate.Column);
        }

        return null;
    }

    private static void ApplyBreak(List<SyntaxToken> tokens, BreakCandidate candidate)
    {
        var removeCount = candidate.RightIndex - candidate.LeftIndex - 1;
        if (removeCount > 0)
        {
            tokens.RemoveRange(candidate.LeftIndex + 1, removeCount);
        }

        var anchor = tokens[candidate.LeftIndex + 1];
        tokens.Insert(candidate.LeftIndex + 1, new SyntaxToken(
            SyntaxKind.NewLine,
            "\n",
            anchor.Offset,
            anchor.Line,
            -1));
        if (candidate.Indentation.Length > 0)
        {
            tokens.Insert(candidate.LeftIndex + 2, new SyntaxToken(
                SyntaxKind.Whitespace,
                candidate.Indentation,
                anchor.Offset,
                anchor.Line,
                -1));
        }
    }

    private static void AddAfterCandidate(
        IReadOnlyList<SyntaxToken> tokens,
        int leftIndex,
        LineInfo line,
        WrapStyle style,
        string structuralIndentation,
        ICollection<BreakCandidate> candidates,
        FormatterOptions options)
    {
        if (style == WrapStyle.Preserve ||
            !TryFindNextCodeOnLine(tokens, leftIndex + 1, out var rightIndex) ||
            tokens[rightIndex].Text is ")" or "]")
        {
            return;
        }

        candidates.Add(new BreakCandidate(
            line,
            leftIndex,
            rightIndex,
            GetEndColumn(tokens, line, leftIndex, options.Indentation.TabWidth),
            style,
            GetContinuationIndentation(line, structuralIndentation, options)));
    }

    private static void AddBeforeCandidate(
        IReadOnlyList<SyntaxToken> tokens,
        int rightIndex,
        LineInfo line,
        WrapStyle style,
        string indentation,
        ICollection<BreakCandidate> candidates)
    {
        if (!TryFindPreviousCodeOnLine(tokens, rightIndex - 1, out var leftIndex) ||
            tokens[leftIndex].Text is "(" or "[")
        {
            return;
        }

        candidates.Add(new BreakCandidate(
            line,
            leftIndex,
            rightIndex,
            0,
            style,
            indentation));
    }

    private static void AddHangingAfterCandidate(
        IReadOnlyList<SyntaxToken> tokens,
        int leftIndex,
        LineInfo line,
        string indentation,
        ICollection<BreakCandidate> candidates)
    {
        if (!TryFindNextCode(tokens, leftIndex + 1, out var rightIndex) ||
            tokens[rightIndex].Text is ")" or "]")
        {
            return;
        }

        if (HasExactBreak(tokens, leftIndex, rightIndex, indentation))
        {
            return;
        }

        candidates.Add(new BreakCandidate(
            line,
            leftIndex,
            rightIndex,
            0,
            WrapStyle.Always,
            indentation));
    }

    private static bool HasExactBreak(
        IReadOnlyList<SyntaxToken> tokens,
        int leftIndex,
        int rightIndex,
        string indentation)
    {
        var index = leftIndex + 1;
        if (index >= rightIndex || tokens[index].Kind != SyntaxKind.NewLine)
        {
            return false;
        }

        index++;
        var actualIndentation = string.Empty;
        while (index < rightIndex && tokens[index].Kind == SyntaxKind.Whitespace)
        {
            actualIndentation += tokens[index].Text;
            index++;
        }

        return index == rightIndex &&
               string.Equals(actualIndentation, indentation, StringComparison.Ordinal);
    }

    private static void AddBinaryCandidate(
        IReadOnlyList<SyntaxToken> tokens,
        int operatorIndex,
        LineInfo line,
        IReadOnlyList<DelimiterScope> scopes,
        ICollection<BreakCandidate> candidates,
        FormatterOptions options)
    {
        var style = options.Wrapping.BinaryExpressions;
        if (style == WrapStyle.Preserve)
        {
            return;
        }

        int leftIndex;
        int rightIndex;
        if (options.Wrapping.BinaryOperatorPosition == BinaryOperatorPosition.After)
        {
            leftIndex = operatorIndex;
            if (!TryFindNextCodeOnLine(tokens, operatorIndex + 1, out rightIndex))
            {
                return;
            }
        }
        else
        {
            rightIndex = operatorIndex;
            if (!TryFindPreviousCodeOnLine(tokens, operatorIndex - 1, out leftIndex))
            {
                return;
            }
        }

        candidates.Add(new BreakCandidate(
            line,
            leftIndex,
            rightIndex,
            GetEndColumn(tokens, line, leftIndex, options.Indentation.TabWidth),
            style,
            GetContinuationIndentation(
                line,
                scopes.Count > 0 ? scopes[0].BaseIndentation : null,
                options)));
    }

    private static DelimiterScope CreateScope(
        IReadOnlyList<SyntaxToken> tokens,
        int openingIndex,
        SyntaxToken? previous,
        LineInfo line,
        IReadOnlyList<DelimiterScope> scopes,
        FormatterOptions options)
    {
        var kind = tokens[openingIndex].Text switch
        {
            "(" when previous?.Text == ":=" && LooksLikeStructureInitializer(tokens, openingIndex) =>
                DelimiterKind.Initializer,
            "(" when IsCallable(previous) => DelimiterKind.Call,
            "[" when previous?.Text == ":=" => DelimiterKind.Initializer,
            "[" when previous?.Text is "[" or "," && scopes.LastOrDefault()?.Kind == DelimiterKind.Initializer =>
                DelimiterKind.Initializer,
            _ => DelimiterKind.Other
        };
        var style = kind switch
        {
            DelimiterKind.Call => options.Wrapping.Calls,
            DelimiterKind.Initializer => options.Wrapping.Initializers,
            _ => WrapStyle.Preserve
        };

        var structuralIndentation = scopes.Count > 0
            ? scopes[0].BaseIndentation
            : line.LeadingWhitespace;
        var useHangingIndentation = kind == DelimiterKind.Call &&
                                    style == WrapStyle.Hanging &&
                                    HasMultipleItems(tokens, openingIndex) &&
                                    HasFirstItemOnOpeningLine(tokens, openingIndex) &&
                                    (ContainsLineBreak(tokens, openingIndex) ||
                                     options.Layout.MaximumLineLength > 0 &&
                                     line.Width > options.Layout.MaximumLineLength);
        return new DelimiterScope(
            kind,
            style,
            structuralIndentation,
            GetHangingIndentation(tokens, line, openingIndex, options.Indentation.TabWidth),
            useHangingIndentation);
    }

    private static bool ShouldBreakAfterOpeningDelimiter(
        IReadOnlyList<SyntaxToken> tokens,
        int openingIndex,
        LineInfo line,
        DelimiterScope scope,
        FormatterOptions options)
    {
        if (scope.Style != WrapStyle.WhenLong || !HasMultipleItems(tokens, openingIndex))
        {
            return false;
        }

        return ContainsLineBreak(tokens, openingIndex) ||
               options.Layout.MaximumLineLength > 0 &&
               line.Width > options.Layout.MaximumLineLength;
    }

    private static bool HasMultipleItems(IReadOnlyList<SyntaxToken> tokens, int openingIndex)
    {
        var depth = 0;
        for (var index = openingIndex + 1; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Kind is SyntaxKind.LineComment or SyntaxKind.BlockComment)
            {
                continue;
            }

            if (token.Text is "(" or "[")
            {
                depth++;
                continue;
            }

            if (token.Text is ")" or "]")
            {
                if (depth == 0)
                {
                    return false;
                }

                depth--;
                continue;
            }

            if (depth == 0 && token.Text == ",")
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasFirstItemOnOpeningLine(
        IReadOnlyList<SyntaxToken> tokens,
        int openingIndex) =>
        TryFindNextCodeOnLine(tokens, openingIndex + 1, out var firstItemIndex) &&
        tokens[firstItemIndex].Text is not ")" and not "]";

    private static bool ContainsLineBreak(IReadOnlyList<SyntaxToken> tokens, int openingIndex)
    {
        var depth = 0;
        for (var index = openingIndex + 1; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Kind == SyntaxKind.NewLine)
            {
                return true;
            }

            if (token.Kind is SyntaxKind.LineComment or SyntaxKind.BlockComment)
            {
                continue;
            }

            if (token.Text is "(" or "[")
            {
                depth++;
            }
            else if (token.Text is ")" or "]")
            {
                if (depth == 0)
                {
                    return false;
                }

                depth--;
            }
        }

        return false;
    }

    private static bool LooksLikeStructureInitializer(IReadOnlyList<SyntaxToken> tokens, int openingIndex)
    {
        var depth = 0;
        for (var index = openingIndex + 1; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Kind is SyntaxKind.LineComment or SyntaxKind.BlockComment)
            {
                continue;
            }

            if (token.Text is "(" or "[")
            {
                depth++;
                continue;
            }

            if (token.Text is ")" or "]")
            {
                if (depth == 0)
                {
                    return false;
                }

                depth--;
                continue;
            }

            if (depth == 0 && token.Text == ":=")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCallable(SyntaxToken? token)
    {
        if (token is null || token.Text is ")" or "]")
        {
            return token is not null;
        }

        if (token.Kind == SyntaxKind.Identifier)
        {
            return true;
        }

        if (token.Kind != SyntaxKind.Keyword)
        {
            return false;
        }

        return token.Text.ToUpperInvariant() is not
            ("IF" or "ELSIF" or "WHILE" or "UNTIL" or "FOR" or "CASE" or "REPEAT");
    }

    private static bool IsBinaryOperator(IReadOnlyList<SyntaxToken> tokens, int index)
    {
        var text = tokens[index].Text.ToUpperInvariant();
        if (text is not ("=" or "<" or ">" or "<=" or ">=" or "<>" or "?=" or
            "+" or "-" or "*" or "/" or "**" or "&" or
            "AND" or "AND_THEN" or "OR" or "OR_ELSE" or "XOR" or "MOD"))
        {
            return false;
        }

        if (text is not ("+" or "-"))
        {
            return true;
        }

        if (!TryFindPreviousCodeOnLine(tokens, index - 1, out var previousIndex))
        {
            return false;
        }

        var previous = tokens[previousIndex].Text.ToUpperInvariant();
        return previous is not ("(" or "[" or "," or ":" or ":=" or "=>" or "REF=" or "=" or
            "<" or ">" or "<=" or ">=" or "<>" or "+" or "-" or "*" or "/" or "**" or "&" or
            "AND" or "AND_THEN" or "OR" or "OR_ELSE" or "XOR" or "MOD" or "THEN" or "DO" or "OF");
    }

    private static IReadOnlyList<LineInfo> CreateLines(IReadOnlyList<SyntaxToken> tokens, int tabWidth)
    {
        var lines = new List<LineInfo>();
        var start = 0;
        var width = 0;
        var containsMultilineToken = false;

        for (var index = 0; index <= tokens.Count; index++)
        {
            if (index < tokens.Count && tokens[index].Kind != SyntaxKind.NewLine)
            {
                containsMultilineToken |= tokens[index].Text.AsSpan().IndexOfAny('\r', '\n') >= 0;
                width = AddWidth(width, tokens[index].Text, tabWidth);
                continue;
            }

            var leadingWhitespace = start < index && tokens[start].Kind == SyntaxKind.Whitespace
                ? tokens[start].Text
                : string.Empty;
            var syntheticIndentation = start < index &&
                                       tokens[start].Kind == SyntaxKind.Whitespace &&
                                       tokens[start].Column == -1;
            lines.Add(new LineInfo(
                start,
                index,
                width,
                leadingWhitespace,
                syntheticIndentation,
                containsMultilineToken));
            start = index + 1;
            width = 0;
            containsMultilineToken = false;
        }

        return lines;
    }

    private static int GetEndColumn(
        IReadOnlyList<SyntaxToken> tokens,
        LineInfo line,
        int inclusiveEnd,
        int tabWidth)
    {
        var width = 0;
        for (var index = line.Start; index <= inclusiveEnd; index++)
        {
            width = AddWidth(width, tokens[index].Text, tabWidth);
        }

        return width;
    }

    private static int AddWidth(int width, string text, int tabWidth)
    {
        foreach (var character in text)
        {
            width = character == '\t'
                ? width + tabWidth - width % tabWidth
                : width + 1;
        }

        return width;
    }

    private static string GetContinuationIndentation(
        LineInfo line,
        string? structuralIndentation,
        FormatterOptions options)
    {
        var continuation = options.Indentation.Style == IndentStyle.Tabs &&
                           options.Indentation.ContinuationSize == options.Indentation.TabWidth
            ? "\t"
            : new string(' ', options.Indentation.ContinuationSize);
        if (structuralIndentation is not null)
        {
            return structuralIndentation + continuation;
        }

        return line.SyntheticIndentation
            ? line.LeadingWhitespace
            : line.LeadingWhitespace + continuation;
    }

    private static string GetHangingIndentation(
        IReadOnlyList<SyntaxToken> tokens,
        LineInfo line,
        int openingIndex,
        int tabWidth)
    {
        var targetColumn = GetEndColumn(tokens, line, openingIndex, tabWidth);
        var leadingWidth = AddWidth(0, line.LeadingWhitespace, tabWidth);
        return line.LeadingWhitespace + new string(' ', Math.Max(0, targetColumn - leadingWidth));
    }

    private static bool TryFindNextCodeOnLine(
        IReadOnlyList<SyntaxToken> tokens,
        int start,
        out int index)
    {
        for (index = start; index < tokens.Count; index++)
        {
            if (tokens[index].Kind == SyntaxKind.NewLine)
            {
                return false;
            }

            if (tokens[index].Kind == SyntaxKind.Whitespace)
            {
                continue;
            }

            return tokens[index].Kind is not SyntaxKind.LineComment and not SyntaxKind.BlockComment;
        }

        return false;
    }

    private static bool TryFindNextCode(
        IReadOnlyList<SyntaxToken> tokens,
        int start,
        out int index)
    {
        for (index = start; index < tokens.Count; index++)
        {
            if (tokens[index].Kind is SyntaxKind.Whitespace or SyntaxKind.NewLine)
            {
                continue;
            }

            return tokens[index].Kind is not SyntaxKind.LineComment and not SyntaxKind.BlockComment;
        }

        return false;
    }

    private static bool TryFindPreviousCodeOnLine(
        IReadOnlyList<SyntaxToken> tokens,
        int start,
        out int index)
    {
        for (index = start; index >= 0; index--)
        {
            if (tokens[index].Kind == SyntaxKind.NewLine)
            {
                return false;
            }

            if (tokens[index].Kind == SyntaxKind.Whitespace)
            {
                continue;
            }

            return tokens[index].Kind is not SyntaxKind.LineComment and not SyntaxKind.BlockComment;
        }

        return false;
    }

    private static bool AllWrappingIsPreserved(FormatterOptions options) =>
        options.Wrapping.Calls == WrapStyle.Preserve &&
        options.Wrapping.Initializers == WrapStyle.Preserve &&
        options.Wrapping.BinaryExpressions == WrapStyle.Preserve;

    private sealed record LayoutAnalysis(
        IReadOnlyList<LineInfo> Lines,
        IReadOnlyList<BreakCandidate> Candidates);

    private sealed record LineInfo(
        int Start,
        int End,
        int Width,
        string LeadingWhitespace,
        bool SyntheticIndentation,
        bool ContainsMultilineToken);

    private sealed record BreakCandidate(
        LineInfo Line,
        int LeftIndex,
        int RightIndex,
        int Column,
        WrapStyle Style,
        string Indentation);

    private sealed record DelimiterScope(
        DelimiterKind Kind,
        WrapStyle Style,
        string BaseIndentation,
        string HangingIndentation,
        bool UseHangingIndentation);

    private enum DelimiterKind
    {
        Other,
        Call,
        Initializer
    }
}
