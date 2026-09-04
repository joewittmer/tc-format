namespace TcFormat.Core;

public static class EditorConfigOptionCatalog
{
    public static IReadOnlyDictionary<string, string> BuiltInValues { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["indent_style"] = "space",
            ["indent_size"] = "4",
            ["tab_width"] = "4",
            ["end_of_line"] = "crlf",
            ["insert_final_newline"] = "true",
            ["trim_trailing_whitespace"] = "true",
            ["max_line_length"] = "110",
            ["tc_format_keyword_case"] = "upper",
            ["tc_format_continuation_indent_size"] = "4",
            ["tc_format_indent_case_labels"] = "true",
            ["tc_format_one_statement_per_line"] = "true",
            ["tc_format_max_consecutive_blank_lines"] = "1",
            ["tc_format_blank_line_before_var"] = "true",
            ["tc_format_blank_line_before_if"] = "false",
            ["tc_format_blank_line_before_case"] = "false",
            ["tc_format_blank_line_before_if_else"] = "false",
            ["tc_format_blank_line_before_case_else"] = "true",
            ["tc_format_blank_line_before_elsif"] = "false",
            ["tc_format_blank_line_before_case_label"] = "false",
            ["tc_format_blank_line_before_end_var"] = "false",
            ["tc_format_blank_line_before_end_if"] = "false",
            ["tc_format_blank_line_before_end_case"] = "false",
            ["tc_format_blank_line_after_if_then"] = "false",
            ["tc_format_blank_line_after_elsif_then"] = "false",
            ["tc_format_blank_line_after_do"] = "false",
            ["tc_format_blank_line_after_case_label"] = "false",
            ["tc_format_align_declarations"] = "true",
            ["tc_format_align_declaration_initializers"] = "true",
            ["tc_format_align_assignments"] = "true",
            ["tc_format_align_named_inputs"] = "true",
            ["tc_format_align_named_outputs"] = "true",
            ["tc_format_align_addresses"] = "true",
            ["tc_format_align_end_of_line_comments"] = "false",
            ["tc_format_wrap_calls"] = "hanging",
            ["tc_format_wrap_initializers"] = "when_long",
            ["tc_format_wrap_binary_expressions"] = "when_long",
            ["tc_format_binary_operator_position"] = "before",
            ["tc_format_space_before_declaration_colon"] = "true",
            ["tc_format_space_after_declaration_colon"] = "true",
            ["tc_format_space_around_assignment_operators"] = "true",
            ["tc_format_space_around_named_argument_operators"] = "true",
            ["tc_format_space_around_binary_operators"] = "true",
            ["tc_format_space_around_comparison_operators"] = "true",
            ["tc_format_space_around_range_operator"] = "false",
            ["tc_format_space_after_comma"] = "true",
            ["tc_format_space_inside_parentheses"] = "false",
            ["tc_format_space_inside_brackets"] = "false",
            ["tc_format_spaces_before_end_of_line_comment"] = "1"
        };
}

