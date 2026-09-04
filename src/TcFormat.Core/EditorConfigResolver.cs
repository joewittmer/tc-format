using EditorConfig.Core;

namespace TcFormat.Core;

public sealed class EditorConfigResolver
{
    private const string BuiltInSource = "<built-in>";

    private readonly EditorConfigParser parser;

    public EditorConfigResolver()
        : this(new EditorConfigParser())
    {
    }

    internal EditorConfigResolver(EditorConfigParser parser)
    {
        this.parser = parser;
    }

    public ResolvedFormatterConfiguration Resolve(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        var values = EditorConfigOptionCatalog.BuiltInValues.ToDictionary(
            pair => pair.Key,
            pair => new MutableResolvedValue(pair.Value, BuiltInSource, null),
            StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<ConfigurationDiagnostic>();

        foreach (var configFile in parser.GetConfigurationFilesTillRoot(fullPath))
        {
            var sourcePath = Path.Combine(configFile.Directory, configFile.FileName);
            var partial = parser.Parse(fullPath, [configFile]);

            foreach (var property in partial.Properties)
            {
                if (!EditorConfigOptionCatalog.BuiltInValues.ContainsKey(property.Key))
                {
                    continue;
                }

                if (string.Equals(property.Value, "unset", StringComparison.OrdinalIgnoreCase))
                {
                    values[property.Key] = new MutableResolvedValue(
                        EditorConfigOptionCatalog.BuiltInValues[property.Key],
                        BuiltInSource,
                        sourcePath);
                    continue;
                }

                values[property.Key] = new MutableResolvedValue(property.Value, sourcePath, null);
            }
        }

        var options = ParseOptions(values, diagnostics);
        diagnostics.AddRange(options.Validate().Select(message => new ConfigurationDiagnostic(message)));

        var resolvedValues = EditorConfigOptionCatalog.BuiltInValues.Keys
            .Select(name => new ResolvedOptionValue(
                name,
                values[name].Value,
                values[name].Source,
                values[name].UnsetBy))
            .ToArray();

        return new ResolvedFormatterConfiguration(fullPath, options, resolvedValues, diagnostics);
    }

    private static FormatterOptions ParseOptions(
        IReadOnlyDictionary<string, MutableResolvedValue> values,
        ICollection<ConfigurationDiagnostic> diagnostics)
    {
        var defaults = FormatterOptions.Default;

        var tabWidth = ReadPositiveInt("tab_width", defaults.Indentation.TabWidth, values, diagnostics);
        var indentSize = ReadIndentSize("indent_size", defaults.Indentation.Size, tabWidth, values, diagnostics);
        return new FormatterOptions(
            KeywordCase: ReadEnum(
                "tc_format_keyword_case",
                defaults.KeywordCase,
                values,
                diagnostics,
                ("upper", KeywordCase.Upper),
                ("lower", KeywordCase.Lower),
                ("preserve", KeywordCase.Preserve)),
            File: new FileOptions(
                EndOfLine: ReadEnum(
                    "end_of_line",
                    defaults.File.EndOfLine,
                    values,
                    diagnostics,
                    ("crlf", EndOfLineStyle.CrLf),
                    ("lf", EndOfLineStyle.Lf),
                    ("cr", EndOfLineStyle.Cr)),
                InsertFinalNewline: ReadBoolean(
                    "insert_final_newline",
                    defaults.File.InsertFinalNewline,
                    values,
                    diagnostics),
                TrimTrailingWhitespace: ReadBoolean(
                    "trim_trailing_whitespace",
                    defaults.File.TrimTrailingWhitespace,
                    values,
                    diagnostics)),
            Indentation: new IndentationOptions(
                Style: ReadEnum(
                    "indent_style",
                    defaults.Indentation.Style,
                    values,
                    diagnostics,
                    ("space", IndentStyle.Spaces),
                    ("tab", IndentStyle.Tabs)),
                Size: indentSize,
                TabWidth: tabWidth,
                ContinuationSize: ReadPositiveInt(
                    "tc_format_continuation_indent_size",
                    defaults.Indentation.ContinuationSize,
                    values,
                    diagnostics),
                IndentCaseLabels: ReadBoolean(
                    "tc_format_indent_case_labels",
                    defaults.Indentation.IndentCaseLabels,
                    values,
                    diagnostics)),
            Layout: new LayoutOptions(
                MaximumLineLength: ReadMaximumLineLength(
                    "max_line_length",
                    defaults.Layout.MaximumLineLength,
                    values,
                    diagnostics),
                OneStatementPerLine: ReadBoolean(
                    "tc_format_one_statement_per_line",
                    defaults.Layout.OneStatementPerLine,
                    values,
                    diagnostics),
                MaximumConsecutiveBlankLines: ReadNonNegativeInt(
                    "tc_format_max_consecutive_blank_lines",
                    defaults.Layout.MaximumConsecutiveBlankLines,
                    values,
                    diagnostics)),
            BlankLines: new BlankLineOptions(
                BeforeVariableBlock: ReadBlankLinePolicy(
                    "tc_format_blank_line_before_var",
                    defaults.BlankLines.BeforeVariableBlock,
                    values,
                    diagnostics),
                BeforeIf: ReadBlankLinePolicy(
                    "tc_format_blank_line_before_if",
                    defaults.BlankLines.BeforeIf,
                    values,
                    diagnostics),
                BeforeCase: ReadBlankLinePolicy(
                    "tc_format_blank_line_before_case",
                    defaults.BlankLines.BeforeCase,
                    values,
                    diagnostics),
                BeforeIfElse: ReadBlankLinePolicy(
                    "tc_format_blank_line_before_if_else",
                    defaults.BlankLines.BeforeIfElse,
                    values,
                    diagnostics),
                BeforeCaseElse: ReadBlankLinePolicy(
                    "tc_format_blank_line_before_case_else",
                    defaults.BlankLines.BeforeCaseElse,
                    values,
                    diagnostics),
                BeforeElsif: ReadBlankLinePolicy(
                    "tc_format_blank_line_before_elsif",
                    defaults.BlankLines.BeforeElsif,
                    values,
                    diagnostics),
                BeforeCaseLabel: ReadBlankLinePolicy(
                    "tc_format_blank_line_before_case_label",
                    defaults.BlankLines.BeforeCaseLabel,
                    values,
                    diagnostics),
                BeforeEndVar: ReadBlankLinePolicy(
                    "tc_format_blank_line_before_end_var",
                    defaults.BlankLines.BeforeEndVar,
                    values,
                    diagnostics),
                BeforeEndIf: ReadBlankLinePolicy(
                    "tc_format_blank_line_before_end_if",
                    defaults.BlankLines.BeforeEndIf,
                    values,
                    diagnostics),
                BeforeEndCase: ReadBlankLinePolicy(
                    "tc_format_blank_line_before_end_case",
                    defaults.BlankLines.BeforeEndCase,
                    values,
                    diagnostics),
                AfterIfThen: ReadBlankLinePolicy(
                    "tc_format_blank_line_after_if_then",
                    defaults.BlankLines.AfterIfThen,
                    values,
                    diagnostics),
                AfterElsifThen: ReadBlankLinePolicy(
                    "tc_format_blank_line_after_elsif_then",
                    defaults.BlankLines.AfterElsifThen,
                    values,
                    diagnostics),
                AfterDo: ReadBlankLinePolicy(
                    "tc_format_blank_line_after_do",
                    defaults.BlankLines.AfterDo,
                    values,
                    diagnostics),
                AfterCaseLabel: ReadBlankLinePolicy(
                    "tc_format_blank_line_after_case_label",
                    defaults.BlankLines.AfterCaseLabel,
                    values,
                    diagnostics)),
            Alignment: new AlignmentOptions(
                Declarations: ReadBoolean(
                    "tc_format_align_declarations",
                    defaults.Alignment.Declarations,
                    values,
                    diagnostics),
                DeclarationInitializers: ReadBoolean(
                    "tc_format_align_declaration_initializers",
                    defaults.Alignment.DeclarationInitializers,
                    values,
                    diagnostics),
                Assignments: ReadBoolean(
                    "tc_format_align_assignments",
                    defaults.Alignment.Assignments,
                    values,
                    diagnostics),
                NamedInputs: ReadBoolean(
                    "tc_format_align_named_inputs",
                    defaults.Alignment.NamedInputs,
                    values,
                    diagnostics),
                NamedOutputs: ReadBoolean(
                    "tc_format_align_named_outputs",
                    defaults.Alignment.NamedOutputs,
                    values,
                    diagnostics),
                Addresses: ReadBoolean(
                    "tc_format_align_addresses",
                    defaults.Alignment.Addresses,
                    values,
                    diagnostics),
                EndOfLineComments: ReadBoolean(
                    "tc_format_align_end_of_line_comments",
                    defaults.Alignment.EndOfLineComments,
                    values,
                    diagnostics)),
            Wrapping: new WrappingOptions(
                Calls: ReadCallWrapStyle(
                    "tc_format_wrap_calls",
                    defaults.Wrapping.Calls,
                    values,
                    diagnostics),
                Initializers: ReadWrapStyle(
                    "tc_format_wrap_initializers",
                    defaults.Wrapping.Initializers,
                    values,
                    diagnostics),
                BinaryExpressions: ReadWrapStyle(
                    "tc_format_wrap_binary_expressions",
                    defaults.Wrapping.BinaryExpressions,
                    values,
                    diagnostics),
                BinaryOperatorPosition: ReadEnum(
                    "tc_format_binary_operator_position",
                    defaults.Wrapping.BinaryOperatorPosition,
                    values,
                    diagnostics,
                    ("before", BinaryOperatorPosition.Before),
                    ("after", BinaryOperatorPosition.After))),
            Spacing: new SpacingOptions(
                BeforeDeclarationColon: ReadBoolean(
                    "tc_format_space_before_declaration_colon",
                    defaults.Spacing.BeforeDeclarationColon,
                    values,
                    diagnostics),
                AfterDeclarationColon: ReadBoolean(
                    "tc_format_space_after_declaration_colon",
                    defaults.Spacing.AfterDeclarationColon,
                    values,
                    diagnostics),
                AroundAssignmentOperators: ReadBoolean(
                    "tc_format_space_around_assignment_operators",
                    defaults.Spacing.AroundAssignmentOperators,
                    values,
                    diagnostics),
                AroundNamedArgumentOperators: ReadBoolean(
                    "tc_format_space_around_named_argument_operators",
                    defaults.Spacing.AroundNamedArgumentOperators,
                    values,
                    diagnostics),
                AroundBinaryOperators: ReadBoolean(
                    "tc_format_space_around_binary_operators",
                    defaults.Spacing.AroundBinaryOperators,
                    values,
                    diagnostics),
                AroundComparisonOperators: ReadBoolean(
                    "tc_format_space_around_comparison_operators",
                    defaults.Spacing.AroundComparisonOperators,
                    values,
                    diagnostics),
                AroundRangeOperator: ReadBoolean(
                    "tc_format_space_around_range_operator",
                    defaults.Spacing.AroundRangeOperator,
                    values,
                    diagnostics),
                AfterComma: ReadBoolean(
                    "tc_format_space_after_comma",
                    defaults.Spacing.AfterComma,
                    values,
                    diagnostics),
                InsideParentheses: ReadBoolean(
                    "tc_format_space_inside_parentheses",
                    defaults.Spacing.InsideParentheses,
                    values,
                    diagnostics),
                InsideBrackets: ReadBoolean(
                    "tc_format_space_inside_brackets",
                    defaults.Spacing.InsideBrackets,
                    values,
                    diagnostics),
                SpacesBeforeEndOfLineComment: ReadNonNegativeInt(
                    "tc_format_spaces_before_end_of_line_comment",
                    defaults.Spacing.SpacesBeforeEndOfLineComment,
                    values,
                    diagnostics)));
    }

    private static WrapStyle ReadWrapStyle(
        string key,
        WrapStyle fallback,
        IReadOnlyDictionary<string, MutableResolvedValue> values,
        ICollection<ConfigurationDiagnostic> diagnostics) =>
        ReadEnum(
            key,
            fallback,
            values,
            diagnostics,
            ("when_long", WrapStyle.WhenLong),
            ("always", WrapStyle.Always),
            ("preserve", WrapStyle.Preserve));

    private static WrapStyle ReadCallWrapStyle(
        string key,
        WrapStyle fallback,
        IReadOnlyDictionary<string, MutableResolvedValue> values,
        ICollection<ConfigurationDiagnostic> diagnostics) =>
        ReadEnum(
            key,
            fallback,
            values,
            diagnostics,
            ("when_long", WrapStyle.WhenLong),
            ("hanging", WrapStyle.Hanging),
            ("always", WrapStyle.Always),
            ("preserve", WrapStyle.Preserve));

    private static bool ReadBoolean(
        string key,
        bool fallback,
        IReadOnlyDictionary<string, MutableResolvedValue> values,
        ICollection<ConfigurationDiagnostic> diagnostics)
    {
        var resolved = values[key];
        if (bool.TryParse(resolved.Value, out var value))
        {
            return value;
        }

        AddInvalidValueDiagnostic(key, resolved, "Expected true or false.", diagnostics);
        return fallback;
    }

    private static BlankLinePolicy ReadBlankLinePolicy(
        string key,
        BlankLinePolicy fallback,
        IReadOnlyDictionary<string, MutableResolvedValue> values,
        ICollection<ConfigurationDiagnostic> diagnostics) =>
        ReadEnum(
            key,
            fallback,
            values,
            diagnostics,
            ("true", BlankLinePolicy.Require),
            ("false", BlankLinePolicy.Remove),
            ("preserve", BlankLinePolicy.Preserve));

    private static int ReadIndentSize(
        string key,
        int fallback,
        int tabWidth,
        IReadOnlyDictionary<string, MutableResolvedValue> values,
        ICollection<ConfigurationDiagnostic> diagnostics)
    {
        var resolved = values[key];
        if (string.Equals(resolved.Value, "tab", StringComparison.OrdinalIgnoreCase))
        {
            return tabWidth;
        }

        return ReadPositiveInt(key, fallback, values, diagnostics);
    }

    private static int ReadMaximumLineLength(
        string key,
        int fallback,
        IReadOnlyDictionary<string, MutableResolvedValue> values,
        ICollection<ConfigurationDiagnostic> diagnostics)
    {
        var resolved = values[key];
        if (string.Equals(resolved.Value, "off", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return ReadPositiveInt(key, fallback, values, diagnostics);
    }

    private static int ReadPositiveInt(
        string key,
        int fallback,
        IReadOnlyDictionary<string, MutableResolvedValue> values,
        ICollection<ConfigurationDiagnostic> diagnostics)
    {
        var resolved = values[key];
        if (int.TryParse(resolved.Value, out var value) && value > 0)
        {
            return value;
        }

        AddInvalidValueDiagnostic(key, resolved, "Expected an integer greater than zero.", diagnostics);
        return fallback;
    }

    private static int ReadNonNegativeInt(
        string key,
        int fallback,
        IReadOnlyDictionary<string, MutableResolvedValue> values,
        ICollection<ConfigurationDiagnostic> diagnostics)
    {
        var resolved = values[key];
        if (int.TryParse(resolved.Value, out var value) && value >= 0)
        {
            return value;
        }

        AddInvalidValueDiagnostic(key, resolved, "Expected a non-negative integer.", diagnostics);
        return fallback;
    }

    private static T ReadEnum<T>(
        string key,
        T fallback,
        IReadOnlyDictionary<string, MutableResolvedValue> values,
        ICollection<ConfigurationDiagnostic> diagnostics,
        params (string Name, T Value)[] allowed)
        where T : struct, Enum
    {
        var resolved = values[key];
        foreach (var candidate in allowed)
        {
            if (string.Equals(resolved.Value, candidate.Name, StringComparison.OrdinalIgnoreCase))
            {
                return candidate.Value;
            }
        }

        var expected = string.Join(", ", allowed.Select(candidate => candidate.Name));
        AddInvalidValueDiagnostic(key, resolved, $"Expected one of: {expected}.", diagnostics);
        return fallback;
    }

    private static void AddInvalidValueDiagnostic(
        string key,
        MutableResolvedValue resolved,
        string expectation,
        ICollection<ConfigurationDiagnostic> diagnostics) =>
        diagnostics.Add(new ConfigurationDiagnostic(
            $"Invalid value '{resolved.Value}'. {expectation}",
            resolved.Source == BuiltInSource ? null : resolved.Source,
            key));

    private sealed record MutableResolvedValue(string Value, string Source, string? UnsetBy);
}

