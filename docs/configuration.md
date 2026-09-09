# EditorConfig configuration

[Back to README](../README.md)

`tc_format` uses `.editorconfig` as its only project configuration format. It
recognizes a focused set of standard EditorConfig properties plus formatter
properties prefixed with `tc_format_`.

## Less whitespace or more whitespace

Choose a complete profile and copy it to your Structured Text project root as
`.editorconfig`:

- [Less whitespace](../examples/less-whitespace.editorconfig) keeps control-flow
  boundaries tight, removing blank lines at those boundaries.
- [More whitespace](../examples/more-whitespace.editorconfig) inserts one blank
  line before control-flow headers, around case labels and closing control-flow
  keywords, and after multiline calls. Short `IF` and `ELSIF` headers stay close
  to ordinary statements; multiline conditions and nested blocks get a separator.
  `FOR` and `WHILE` always get a blank line after `DO`.
  `REPEAT` retains its following blank line. There is **no blank
  line after `ELSE`**, even when its first statement is another `IF` or a loop.
- [More whitespace without assignment alignment](../examples/more-whitespace-without-assignment-alignment.editorconfig)
  uses the same layout, but keeps single spaces around `:=` and `=>` in assignments,
  declaration initializers, and named parameters. It also disables padding before
  declaration colons in `VAR`, `VAR_INPUT`, and other variable blocks.

Use the annotations in your chosen profile to adjust its settings to your
preferences. Each setting includes an explanation, accepted values, and the
built-in default, so you can customize the profile directly in `.editorconfig`.

All profiles use four-space indentation, single spaces around operators,
`always` initializer wrapping, and `hanging` call wrapping.
They retain up to one manually inserted blank line between ordinary statements.
The more-whitespace profile keeps variable declarations compact. These files
take effect only when copied or merged into a discovered `.editorconfig` file;
their example filenames are not selected automatically by the formatter.

Less whitespace:

```iecst
Prepare();
IF ready THEN
    Run();
ELSIF waiting THEN
    Wait();
ELSE
    Stop();
END_IF
Finish();
```

More whitespace:

```iecst
Prepare();

IF ready THEN
    Run();

ELSIF waiting THEN
    Wait();

ELSE
    Stop();

END_IF

Finish();
```

The more-whitespace profile uses these header settings:

```ini
tc_format_blank_line_after_if_then = multiline
tc_format_blank_line_after_elsif_then = multiline
tc_format_blank_line_after_do = true
```

The `multiline` mode adds one blank line after a multiline header and removes it
after a single-line header unless the next block requires a blank line before it.
It uses the final formatted layout, including line breaks
introduced by call or binary-expression wrapping. For example:

```iecst
IF ready THEN
    Run();

ELSIF waiting AND
    connectionHealthy THEN

    Resume();

ELSE
    Stop();

END_IF
```

For example, a loop directly inside an `IF` gets a separator before `FOR` and
after `DO`, while the short inner condition stays close to its assignment:

```iecst
IF axesAreSimulated THEN

    FOR i := 0 TO count - 1 DO

        IF NOT simulated THEN
            axesAreSimulated := FALSE;
            EXIT;

        END_IF

    END_FOR

END_IF
```

The existing `true`, `false`, and `preserve` values also apply when `THEN` or
`DO` appears on a later line. `multiline` is supported only for these three
header settings. `CASE` uses the case-label spacing options; `UNTIL` introduces
a repeat loop's termination condition rather than a following body.

`tc_format_blank_line_after_multiline_call` separates a long standalone call
from the following statement. The more-whitespace profile sets it to `true`;
the less-whitespace profile sets it to `false`. `preserve` retains existing
spacing and is the built-in default. Calls that become multiline through
wrapping also qualify. Short calls remain compact:

```iecst
_axis.Enable();
_axis.Reset();
_axis.MoveAbsolute(Position := targetPosition,
                   Velocity := 20
);

completed := FALSE;
```

Nested calls do not introduce blank lines inside their enclosing statement.
Calls used in declarations, assignments, and conditions are also excluded.
Trailing comments stay attached to the call. The setting does not split
multiple statements on a line when `tc_format_one_statement_per_line = false`.

The profile options remain individually configurable. At a boundary shared by two
policies, `false` takes precedence over `true`; the rule against blank lines
after `ELSE` takes precedence over both. Case-label spacing applies when the
label's colon ends its line. Loop options cover `FOR`, `WHILE`, and `REPEAT`;
they do not add spacing rules for exception-handling keywords or `RETURN`/`EXIT`.

## Annotated profiles

The [less whitespace](../examples/less-whitespace.editorconfig),
[more whitespace](../examples/more-whitespace.editorconfig), and
[more whitespace without assignment alignment](../examples/more-whitespace-without-assignment-alignment.editorconfig) profiles include every
supported option, with its description, accepted values, and built-in default
immediately above the setting. The assigned values select the profile and may
differ from the documented defaults.

These files are the complete option references. Copy any one to your
project root as `.editorconfig`, or merge its section into an existing file.
Read the annotations above each setting when choosing a value that matches
your preferred formatting style; the profiles are starting points you can edit.
Property names and named values are case-insensitive. Tests check all profiles
for complete option coverage and annotations matching the built-in defaults.

The variant without assignment alignment changes only these settings from the
more-whitespace profile:

```ini
tc_format_align_assignments = false
tc_format_align_declaration_initializers = false
tc_format_align_named_inputs = false
tc_format_align_named_outputs = false
tc_format_align_declarations = false
```

It retains the blank lines while avoiding extra padding before `:=` and `=>`:

```iecst
IF ready THEN
    result := TRUE;
    requestedPosition := targetPosition;

END_IF
```

## Expanded multiline arguments

The more-whitespace profile enables expanded multiline arguments:

```ini
tc_format_expand_multiline_arguments = true
tc_format_binary_operator_position = before
```

When an individual argument spans multiple lines, the call places its arguments
below the opening parenthesis and its closing parenthesis on a separate line:

```iecst
pressureSpeedOverride := LREAL_TO_INT(
    100.0 * _clampingPressureVelocity
    / TO_LREAL(_speedCommandOption)
);
```

The option defaults to `false` and is disabled in the less-whitespace profile.
It applies to existing multiline arguments and arguments expanded by wrapping,
including nested calls. It also works with `tc_format_wrap_calls = preserve`.
Several short arguments on separate lines still use the selected call layout:

```iecst
Move(Position := target,
     Velocity := speed
);
```

Operator position remains a separate preference. Inside expanded arguments it
also moves existing operator breaks, even when binary-expression wrapping is
`preserve`. It does not move an operator across a comment. Outside expanded
arguments, operator position controls newly introduced breaks as before.

## Closing parentheses and brackets

Control closing-delimiter placement independently for multiline expressions:

```ini
tc_format_multiline_closing_parenthesis = same_line
tc_format_multiline_closing_bracket = same_line
```

Each option accepts:

- `preserve` (default): keep closing placement chosen by the wrapping style.
- `own_line`: place the closing delimiter on its own line.
- `same_line`: place the closing delimiter after the last item.

For example, `same_line` produces:

```iecst
monitor : FB_Monitor := (
    Name := 'monitor',
    Reset := reset);

sensors := [
    (Name := 'one'),
    (Name := 'two')];
```

With `own_line`, the endings instead look like:

```iecst
monitor : FB_Monitor := (
    Name := 'monitor',
    Reset := reset
);

sensors := [
    (Name := 'one'),
    (Name := 'two')
];
```

These settings override closing placement for multiline calls, expanded
arguments, initializers, and parenthesized or bracketed expressions, including
when wrapping is `preserve`. Single-line expressions stay on one line.
`same_line` retains a separate closing line if joining would cross a comment or
directive. Ordinary inside-parenthesis and inside-bracket spacing still applies.
The less-whitespace profile selects `same_line` for both settings. Both
more-whitespace profiles select `own_line`. The built-in default remains `preserve`.

## Choosing an initializer layout

`tc_format_wrap_initializers` controls the layout of array entries and structure
fields. `tc_format_align_declaration_initializers` only aligns the declaration's
outer `:=` with nearby declarations; it does not align the entries inside it.

Use `hanging` to keep the first entry beside the opening delimiter and align
subsequent entries beneath it:

```iecst
axes := [(Name := 'Axis 1'),
         (Name := 'Axis 2')];
```

Use `always` to put every entry on a continuation line and the closing delimiter
on its own line. This avoids a large alignment offset after a long declaration:

```iecst
axes := [
    (Name := 'Axis 1'),
    (Name := 'Axis 2')
];
```

`when_long` moves the first entry below the opening delimiter for a long or
already multiline initializer, then adds breaks as needed for the width limit.
`preserve` keeps existing breaks and applies ordinary continuation indentation.
The built-in default remains `when_long`; all profiles select `always`.

Hanging layout leaves short single-line initializers alone. When the first item
already starts below the opening delimiter, it keeps ordinary continuation
indentation. It also works with `max_line_length = off` for existing multiline
initializers. As with calls, comments can prevent a break from being rewritten.

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
It is separate from the Structured Text profiles above.
