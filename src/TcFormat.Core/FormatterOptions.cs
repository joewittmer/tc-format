namespace TcFormat.Core;

public enum IndentStyle
{
    Spaces,
    Tabs
}

public enum EndOfLineStyle
{
    CrLf,
    Lf,
    Cr
}

public enum KeywordCase
{
    Upper,
    Lower,
    Preserve
}

public enum WrapStyle
{
    WhenLong,
    Hanging,
    Always,
    Preserve
}

public enum BinaryOperatorPosition
{
    Before,
    After
}

public enum BlankLinePolicy
{
    Remove,
    Require,
    Preserve
}

public sealed record FileOptions(
    EndOfLineStyle EndOfLine,
    bool InsertFinalNewline,
    bool TrimTrailingWhitespace);

public sealed record IndentationOptions(
    IndentStyle Style,
    int Size,
    int TabWidth,
    int ContinuationSize,
    bool IndentCaseLabels);

public sealed record LayoutOptions(
    int MaximumLineLength,
    bool OneStatementPerLine,
    int MaximumConsecutiveBlankLines);

public sealed record BlankLineOptions(
    BlankLinePolicy BeforeVariableBlock,
    BlankLinePolicy BeforeIf,
    BlankLinePolicy BeforeCase,
    BlankLinePolicy BeforeIfElse,
    BlankLinePolicy BeforeCaseElse,
    BlankLinePolicy BeforeElsif,
    BlankLinePolicy BeforeCaseLabel,
    BlankLinePolicy BeforeEndVar,
    BlankLinePolicy BeforeEndIf,
    BlankLinePolicy BeforeEndCase,
    BlankLinePolicy AfterIfThen,
    BlankLinePolicy AfterElsifThen,
    BlankLinePolicy AfterDo,
    BlankLinePolicy AfterCaseLabel);

public sealed record AlignmentOptions(
    bool Declarations,
    bool DeclarationInitializers,
    bool Assignments,
    bool NamedInputs,
    bool NamedOutputs,
    bool Addresses,
    bool EndOfLineComments);

public sealed record WrappingOptions(
    WrapStyle Calls,
    WrapStyle Initializers,
    WrapStyle BinaryExpressions,
    BinaryOperatorPosition BinaryOperatorPosition);

public sealed record SpacingOptions(
    bool BeforeDeclarationColon,
    bool AfterDeclarationColon,
    bool AroundAssignmentOperators,
    bool AroundNamedArgumentOperators,
    bool AroundBinaryOperators,
    bool AroundComparisonOperators,
    bool AroundRangeOperator,
    bool AfterComma,
    bool InsideParentheses,
    bool InsideBrackets,
    int SpacesBeforeEndOfLineComment);

public sealed record FormatterOptions(
    KeywordCase KeywordCase,
    FileOptions File,
    IndentationOptions Indentation,
    LayoutOptions Layout,
    BlankLineOptions BlankLines,
    AlignmentOptions Alignment,
    WrappingOptions Wrapping,
    SpacingOptions Spacing)
{
    public static FormatterOptions Default { get; } = new(
        KeywordCase: KeywordCase.Upper,
        File: new(
            EndOfLine: EndOfLineStyle.CrLf,
            InsertFinalNewline: true,
            TrimTrailingWhitespace: true),
        Indentation: new(
            Style: IndentStyle.Spaces,
            Size: 4,
            TabWidth: 4,
            ContinuationSize: 4,
            IndentCaseLabels: true),
        Layout: new(
            MaximumLineLength: 110,
            OneStatementPerLine: true,
            MaximumConsecutiveBlankLines: 1),
        BlankLines: new(
            BeforeVariableBlock: BlankLinePolicy.Require,
            BeforeIf: BlankLinePolicy.Remove,
            BeforeCase: BlankLinePolicy.Remove,
            BeforeIfElse: BlankLinePolicy.Remove,
            BeforeCaseElse: BlankLinePolicy.Require,
            BeforeElsif: BlankLinePolicy.Remove,
            BeforeCaseLabel: BlankLinePolicy.Remove,
            BeforeEndVar: BlankLinePolicy.Remove,
            BeforeEndIf: BlankLinePolicy.Remove,
            BeforeEndCase: BlankLinePolicy.Remove,
            AfterIfThen: BlankLinePolicy.Remove,
            AfterElsifThen: BlankLinePolicy.Remove,
            AfterDo: BlankLinePolicy.Remove,
            AfterCaseLabel: BlankLinePolicy.Remove),
        Alignment: new(
            Declarations: true,
            DeclarationInitializers: true,
            Assignments: true,
            NamedInputs: true,
            NamedOutputs: true,
            Addresses: true,
            EndOfLineComments: false),
        Wrapping: new(
            Calls: WrapStyle.Hanging,
            Initializers: WrapStyle.WhenLong,
            BinaryExpressions: WrapStyle.WhenLong,
            BinaryOperatorPosition: BinaryOperatorPosition.Before),
        Spacing: new(
            BeforeDeclarationColon: true,
            AfterDeclarationColon: true,
            AroundAssignmentOperators: true,
            AroundNamedArgumentOperators: true,
            AroundBinaryOperators: true,
            AroundComparisonOperators: true,
            AroundRangeOperator: false,
            AfterComma: true,
            InsideParentheses: false,
            InsideBrackets: false,
            SpacesBeforeEndOfLineComment: 1));

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        ValidateEnum(File.EndOfLine, nameof(File.EndOfLine), errors);
        ValidateEnum(Indentation.Style, nameof(Indentation.Style), errors);
        ValidateEnum(KeywordCase, nameof(KeywordCase), errors);
        ValidateEnum(Wrapping.Calls, nameof(Wrapping.Calls), errors);
        ValidateEnum(Wrapping.Initializers, nameof(Wrapping.Initializers), errors);
        ValidateEnum(Wrapping.BinaryExpressions, nameof(Wrapping.BinaryExpressions), errors);
        ValidateEnum(Wrapping.BinaryOperatorPosition, nameof(Wrapping.BinaryOperatorPosition), errors);
        ValidateEnum(BlankLines.BeforeVariableBlock, nameof(BlankLines.BeforeVariableBlock), errors);
        ValidateEnum(BlankLines.BeforeIf, nameof(BlankLines.BeforeIf), errors);
        ValidateEnum(BlankLines.BeforeCase, nameof(BlankLines.BeforeCase), errors);
        ValidateEnum(BlankLines.BeforeIfElse, nameof(BlankLines.BeforeIfElse), errors);
        ValidateEnum(BlankLines.BeforeCaseElse, nameof(BlankLines.BeforeCaseElse), errors);
        ValidateEnum(BlankLines.BeforeElsif, nameof(BlankLines.BeforeElsif), errors);
        ValidateEnum(BlankLines.BeforeCaseLabel, nameof(BlankLines.BeforeCaseLabel), errors);
        ValidateEnum(BlankLines.BeforeEndVar, nameof(BlankLines.BeforeEndVar), errors);
        ValidateEnum(BlankLines.BeforeEndIf, nameof(BlankLines.BeforeEndIf), errors);
        ValidateEnum(BlankLines.BeforeEndCase, nameof(BlankLines.BeforeEndCase), errors);
        ValidateEnum(BlankLines.AfterIfThen, nameof(BlankLines.AfterIfThen), errors);
        ValidateEnum(BlankLines.AfterElsifThen, nameof(BlankLines.AfterElsifThen), errors);
        ValidateEnum(BlankLines.AfterDo, nameof(BlankLines.AfterDo), errors);
        ValidateEnum(BlankLines.AfterCaseLabel, nameof(BlankLines.AfterCaseLabel), errors);

        RequirePositive(Indentation.Size, nameof(Indentation.Size), errors);
        RequirePositive(Indentation.TabWidth, nameof(Indentation.TabWidth), errors);
        RequirePositive(Indentation.ContinuationSize, nameof(Indentation.ContinuationSize), errors);
        RequireNonNegative(Layout.MaximumLineLength, nameof(Layout.MaximumLineLength), errors);
        RequireNonNegative(Layout.MaximumConsecutiveBlankLines, nameof(Layout.MaximumConsecutiveBlankLines), errors);
        RequireNonNegative(Spacing.SpacesBeforeEndOfLineComment, nameof(Spacing.SpacesBeforeEndOfLineComment), errors);

        return errors;
    }

    private static void ValidateEnum<T>(T value, string name, ICollection<string> errors)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            errors.Add($"{name} has an unsupported value: {value}.");
        }
    }

    private static void RequirePositive(int value, string name, ICollection<string> errors)
    {
        if (value <= 0)
        {
            errors.Add($"{name} must be greater than zero.");
        }
    }

    private static void RequireNonNegative(int value, string name, ICollection<string> errors)
    {
        if (value < 0)
        {
            errors.Add($"{name} cannot be negative.");
        }
    }
}

