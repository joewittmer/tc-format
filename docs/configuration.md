# EditorConfig configuration

`tc_format` uses `.editorconfig` as its only project configuration format. It
recognizes a focused set of standard EditorConfig properties plus formatter
properties prefixed with `tc_format_`.

## Opinionated configuration

The annotated profile below is both a starting point and the complete option
reference. Every setting understood by the formatter is included. The comments
immediately above each setting describe its options and default; the assigned
value is our opinionated choice.

The canonical source is
[`examples/.editorconfig`](../examples/.editorconfig). Its complete contents
are mirrored below so they can be copied directly from this guide. Copy the
profile to the root of a Structured Text repository as `.editorconfig`, or
merge its section into an existing file. Property names and named values are
case-insensitive.

<!-- canonical-profile:start -->
```ini
# Stop EditorConfig discovery at this file. This is repository metadata rather
# than a formatter setting. Omit it from a nested .editorconfig.
root = true

# Apply these settings to every file type supported by tc_format.
[*.{st,iecst,TcPOU,TcDUT,TcGVL,TcITF,TcPRG}]

# File and indentation

# Indentation characters.
# Options:
#   space - Indent with spaces.
#   tab - Indent with tabs.
# Default: space
indent_style = space

# Spaces per indentation level.
# Options:
#   positive_integer - Use that many spaces.
#   tab - Use the effective tab_width.
# Default: 4
indent_size = 4

# Visual width of a tab, used to measure line length and alignment.
# Options: positive_integer
# Default: 4
tab_width = 4

# Line-ending style.
# Options:
#   crlf - Windows line endings.
#   lf - Unix line endings.
#   cr - Carriage-return-only line endings.
# Default: crlf
end_of_line = crlf

# End the formatted code region with one newline.
# Options: true, false
# Default: true
insert_final_newline = true

# Remove spaces and tabs immediately before line endings.
# Options: true, false
# Default: true
trim_trailing_whitespace = true

# Soft visual-width limit used by wrapping and alignment.
# Options:
#   positive_integer - Use that visual-width limit.
#   off - Disable width-triggered wrapping and the alignment width guard.
# Default: 110
max_line_length = off

# General behavior

# Case of recognized Structured Text keywords.
# Options:
#   upper - Convert recognized keywords to uppercase.
#   lower - Convert recognized keywords to lowercase.
#   preserve - Keep each keyword's existing spelling.
# Default: upper
# Identifiers, comments, and string literals are never case-converted.
tc_format_keyword_case = upper

# Extra indentation for ordinary continuation lines.
# Options: positive_integer
# Default: 4
# With tabs, a value equal to tab_width produces one tab.
tc_format_continuation_indent_size = 4

# Indent CASE labels one level beneath CASE and their statements one level
# further.
# Options: true, false
# Default: true
tc_format_indent_case_labels = true

# Statement and block layout

# Split adjacent top-level statements after semicolons.
# Options: true, false
# Default: true
tc_format_one_statement_per_line = true

# Maximum empty lines allowed between content lines.
# Options: non_negative_integer
# Default: 1
# 0 also removes separators inserted by the structural layout.
tc_format_max_consecutive_blank_lines = 1

# Blank-line boundary values:
#   true - Require exactly one empty line.
#   false - Remove existing empty lines.
#   preserve - Do not add or remove an empty line at this boundary.
# The global maximum above still applies when a boundary is preserved.

# Blank line before VAR and VAR_* blocks. The METHOD/VAR boundary stays tight.
# Options: true, false, preserve
# Default: true
tc_format_blank_line_before_var = true

# Blank line before IF.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_before_if = false

# Blank line before CASE.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_before_case = false

# Blank line before ELSE in an IF statement.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_before_if_else = false

# Blank line before ELSE in a CASE statement.
# Options: true, false, preserve
# Default: true
tc_format_blank_line_before_case_else = true

# Blank line before ELSIF.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_before_elsif = false

# Blank line before a CASE-arm label.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_before_case_label = false

# Blank line before END_VAR.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_before_end_var = false

# Blank line before END_IF.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_before_end_if = false

# Blank line before END_CASE.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_before_end_case = false

# Blank line after the THEN on an IF line.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_after_if_then = false

# Blank line after the THEN on an ELSIF line.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_after_elsif_then = false

# Blank line after DO on a FOR or WHILE line.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_after_do = false

# Blank line after a CASE-arm label whose colon ends the line.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_after_case_label = false

# Vertical alignment
# Alignment applies to short, compatible groups and is skipped for the entire
# group if padding would exceed an active max_line_length.

# Align declaration colons.
# Options: true, false
# Default: true
# Example: short    : INT; / longName : DINT;
tc_format_align_declarations = true

# Align := initializers in declarations.
# Options: true, false
# Default: true
tc_format_align_declaration_initializers = true

# Align top-level statement assignment operators.
# Options: true, false
# Default: true
tc_format_align_assignments = true

# Align named input := operators within a multiline call.
# Options: true, false
# Default: true
tc_format_align_named_inputs = true

# Align named output => operators within a multiline call.
# Options: true, false
# Default: true
tc_format_align_named_outputs = true

# Align AT keywords in directly addressed declarations.
# Options: true, false
# Default: true
tc_format_align_addresses = true

# Align // comments on contiguous code lines with the same indentation.
# Options: true, false
# Default: false
tc_format_align_end_of_line_comments = true

# Wrapping

# Function and function-block call wrapping.
# Options:
#   when_long - Wrap when max_line_length is exceeded.
#   hanging - Keep the first argument beside the opening parenthesis and align
#             later arguments beneath it when the call is long or multiline.
#   always - Put every argument on a continuation line.
#   preserve - Introduce no new breaks, but normalize existing lines.
# Default: hanging
tc_format_wrap_calls = hanging

# Array and structure initializer wrapping.
# Options:
#   when_long - Wrap when max_line_length is exceeded.
#   always - Put every item on a continuation line.
#   preserve - Introduce no new breaks, but normalize existing lines.
# Default: when_long
tc_format_wrap_initializers = preserve

# Binary-expression wrapping.
# Options:
#   when_long - Wrap when max_line_length is exceeded.
#   always - Break at every configured binary operator.
#   preserve - Introduce no new breaks, but normalize existing lines.
# Default: when_long
tc_format_wrap_binary_expressions = preserve

# Side of an introduced line break on which a binary operator is placed.
# Options:
#   before - Put the operator at the beginning of the continuation line.
#   after - Put the operator at the end of the preceding line.
# Default: before
tc_format_binary_operator_position = after

# Spacing
# Each boolean spacing option inserts one space when true and removes it when
# false. Whitespace inside comments and string literals is never changed.

# Space before a declaration colon.
# Options:
#   true - Produce "value : INT".
#   false - Produce "value: INT".
# Default: true
tc_format_space_before_declaration_colon = true

# Space after a declaration colon.
# Options:
#   true - Produce "value : INT".
#   false - Produce "value :INT".
# Default: true
tc_format_space_after_declaration_colon = true

# Spaces around statement and declaration operators such as := and REF=.
# Options: true, false
# Default: true
tc_format_space_around_assignment_operators = true

# Spaces around named input := and output => operators in calls and
# initializers.
# Options: true, false
# Default: true
tc_format_space_around_named_argument_operators = true

# Spaces around arithmetic and binary operators such as +, -, *, /, **, and &.
# Options: true, false
# Default: true
# Unary signs remain attached.
tc_format_space_around_binary_operators = true

# Spaces around =, <, >, <=, >=, <>, and ?=.
# Options: true, false
# Default: true
tc_format_space_around_comparison_operators = true

# Spaces around the two-dot range operator.
# Options:
#   true - Produce "values[1 .. 10]".
#   false - Produce "values[1..10]".
# Default: false
tc_format_space_around_range_operator = false

# Space after a comma when the next item is on the same line.
# Options: true, false
# Default: true
tc_format_space_after_comma = true

# Spaces immediately inside non-empty parentheses.
# Options:
#   true - Produce "Call( first )".
#   false - Produce "Call(first)".
# Default: false
tc_format_space_inside_parentheses = false

# Spaces immediately inside non-empty brackets.
# Options:
#   true - Produce "values[ index ]".
#   false - Produce "values[index]".
# Default: false
tc_format_space_inside_brackets = false

# Base gap between code and a trailing // comment.
# Options: non_negative_integer
# Default: 1
# Comment alignment may add more spaces to this gap.
tc_format_spaces_before_end_of_line_comment = 1
```
<!-- canonical-profile:end -->

The values selected by this profile are deliberately explicit and do not
always match the `Default:` line in the comments. An automated test keeps this
copy synchronized with the canonical example.

## Discovery, sections, and inheritance

Settings are resolved independently for each source file. EditorConfig files
are read from the filesystem root toward the source file, stopping at a file
containing `root = true`. Later matching sections and files nearer the source
file override earlier values.

Section patterns determine which files receive the settings. The profile's
pattern covers every file type supported by `tc_format`:

```ini
[*.{st,iecst,TcPOU,TcDUT,TcGVL,TcITF,TcPRG}]
```

A value of `unset` removes an inherited setting and restores the formatter's
default value. For example, this restores the default 110-column limit even
if a parent configuration disables it:

```ini
[MachineA/**/*.{st,TcPOU}]
max_line_length = unset
```

Unknown properties, including newer or removed `tc_format_*` names, are ignored
for forward and backward compatibility. Missing supported properties use their
built-in defaults. An invalid value for a recognized property remains an error.
Both normal formatting and `--check` validate the effective configuration
before writing any files.

## Project-specific overrides

A repository can keep shared defaults at its root and override selected
properties in a nested project. Omit `root = true` from the nested file so the
repository settings remain inherited:

```ini
# MachineA/.editorconfig

[*.{st,iecst,TcPOU,TcDUT,TcGVL,TcITF,TcPRG}]
indent_style = tab
indent_size = 4
tab_width = 4
max_line_length = 150

tc_format_align_declarations = false
tc_format_align_declaration_initializers = false
tc_format_align_assignments = false
tc_format_align_addresses = false
```

## Interactions and safety rules

- `max_line_length` is a soft limit shared by wrapping and alignment. The
  formatter does not split indivisible strings, comments, identifiers, or
  direct addresses, so a line can remain longer than the limit.
- Call, initializer, and binary wrapping can be configured independently.
- Named inputs and outputs can be aligned independently; enabling both allows a
  compatible group in one call to share an operator column.
- Comments and literal contents are preserved. Block-comment-adjacent spacing
  is not rewritten, and wrapping does not split comments or strings.
- Invalid source, invalid configuration, and formatter safety-check failures do
  not produce formatted output or file changes.

This repository's root `.editorconfig` configures the formatter's C#
implementation using reusable rules from
[Roslyn's conventions](https://github.com/dotnet/roslyn/blob/main/.editorconfig).
It is separate from the Structured Text profile above.
