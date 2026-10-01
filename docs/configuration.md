# EditorConfig configuration

[Back to README](../README.md)

Start with one of the preconfigured profiles below. You can use it as supplied
or build your own style by following its annotations. `tc_format` reads these
settings from `.editorconfig`; there is no separate profile selector.

Trailing blank lines are removed from each formatted code region.
`insert_final_newline = true` leaves exactly one terminating newline in files;
the XAE extension removes the terminating newline from editor panes so it does
not leave an empty row at the bottom.

## Use a preconfigured profile

Choose one profile, just as in the [README quick start](../README.md#quick-start):

| Profile to copy | Style | Preview |
| --- | --- | --- |
| [Less whitespace](../examples/less-whitespace.editorconfig) | Compact blocks, single spaces without column padding, multiline endings after the last item | [Example 1](#example-1-less-whitespace) |
| [More whitespace](../examples/more-whitespace.editorconfig) | Separated blocks and calls, aligned columns, multiline endings on their own line | [Example 2](#example-2-more-whitespace) |
| [More whitespace without assignment alignment](../examples/more-whitespace-without-assignment-alignment.editorconfig) | More whitespace, with single spaces around declaration colons and assignment / named parameter operators | [Example 3](#example-3-more-whitespace-without-assignment-alignment) |

Place `.editorconfig` beside your PLC project (`.plcproj`), or in a parent folder
covering the projects you want to format:

- **New file:** copy the entire chosen profile and name it `.editorconfig`.
- **Existing file:** copy the section beginning with
  `[*.{st,iecst,TcPOU,TcDUT,TcGVL,TcITF,TcPRG}]` and everything below it. If the
  section already exists, merge the settings and replace matching properties.
  Preserve unrelated sections and the existing `root` setting.

Omit `root = true` from a new nested file when you want to inherit parent
settings. The example filenames themselves are not discovered automatically.
The installer and portable archive also include all profiles in `examples`.

You can format immediately with the chosen profile. For a custom style, continue
with the walkthrough below; the [profile examples](#profile-examples) show the
starting layouts.

## Build your own configuration

### 1. Start with the closest profile

Copy a profile using the instructions above, then edit your project's
`.editorconfig`. Each profile contains every supported option, so you can work
through it from top to bottom without assembling a configuration from scratch.
You can also copy only selected settings; omitted properties inherit a matching
parent value or use the built-in default when none is inherited.

### 2. Read the scope annotations

The opening comments explain where to put the file. `root = true` stops discovery
of parent EditorConfig files; it is file metadata and belongs before any section.
The `[*.{st,iecst,TcPOU,TcDUT,TcGVL,TcITF,TcPRG}]` header selects the source file
types that receive the following settings. Keep formatter settings under this
header, separate from rules for other languages. See
[discovery and inheritance](#discovery-sections-and-inheritance) for overrides.

### 3. Read each setting's annotation

For example, the more-whitespace profile includes:

```ini
# Blank line before IF.
# Options: true, false, preserve
# Default: false
tc_format_blank_line_before_if = true
```

Read these four parts in order:

1. **Description:** what the setting changes. Here, it controls a blank line
   immediately before `IF`.
2. **Options:** the accepted values. Here, `true` requests a blank line, `false`
   removes one, and `preserve` keeps the existing boundary spacing, subject to
   the maximum blank-line count.
3. **Default:** the formatter's fallback when no effective value is configured.
   It documents the built-in behavior, not the selected profile value.
4. **Assignment:** the active value after `=`. This profile selects `true` even
   though the built-in default is `false`. Change this value to customize it.

Lines beginning with `#` are comments. Editing a `# Default:` comment does not
change formatting; edit the assignment below it. Keep the annotations alongside
the setting so they remain available as help.

### 4. Follow the value explanations and special notes

Some `Options` annotations expand each value onto its own comment line. Use those
explanations when choosing modes such as `hanging`, `multiline`, or `own_line`;
`preserve` has a setting-specific meaning, described beside that option.

`positive_integer` means a whole number of at least 1; `non_negative_integer`
also allows 0. These labels describe a value to enter, not literal settings.
For example, use `max_line_length = 120`, not `max_line_length = positive_integer`.
Named values such as `off`, `true`, and `same_line` are entered literally.
Property names and named values are case-insensitive.

Read any extra notes or examples before the assignment too. They explain limits
and interactions, such as comments preventing a closing delimiter from joining
the preceding line. Group comments apply to all settings in that group.

### 5. Work through the annotated groups

Follow these groups in the same order as the profile. Change only the values
where you want a different result.

| Annotation group | What to choose |
| --- | --- |
| **File and indentation** | Spaces or tabs, indentation and tab widths, line endings, final newline, trailing whitespace, and a soft line-length limit. `off` disables width-triggered wrapping. |
| **General behavior** | Keyword case, continuation indentation, and whether `CASE` labels are indented. Identifiers and string contents retain their spelling. |
| **Statement and block layout** | One statement per line, the maximum blank-line count, and individual boundaries before or after control-flow keywords and multiline calls. The group comments explain `true`, `false`, and `preserve`; only the three `THEN` / `DO` settings also accept `multiline`. See [blank-line behavior](#blank-line-behavior). |
| **Vertical alignment** | Padding for declaration colons, initializers, assignments, named inputs and outputs, direct addresses, and trailing comments. Direct-address padding follows declaration alignment; the other groups can be enabled independently. See [alignment preferences](#alignment-preferences). |
| **Wrapping** | Call, initializer, and binary-expression layouts, expanded multiline arguments, operator position, and multiline closing `)` / `]` placement. See [expanded arguments](#expanded-multiline-arguments), [closing delimiters](#closing-parentheses-and-brackets), and [initializer layouts](#choosing-an-initializer-layout). |
| **Spacing** | Single spaces around punctuation and operators, inside parentheses or brackets, and before trailing comments. Disabling column alignment does not remove these ordinary spaces. |

### 6. Edit the active values

For example, to customize more whitespace with a 120-column soft limit and
multiline closings after the last item, replace these existing assignments in
your copied profile:

```ini
max_line_length = 120
tc_format_multiline_closing_parenthesis = same_line
tc_format_multiline_closing_bracket = same_line
```

Keep the remaining settings. For an override that applies only to a subfolder,
see [project-specific overrides](#project-specific-overrides).

### 7. Check the result on a representative file

From your project folder, check whether a file needs formatting:

```powershell
tc_format --check "POUs\MAIN.TcPOU"
```

`--check` validates the configuration and reports formatting differences without
writing files. To apply your style and inspect the resulting source diff, run:

```powershell
tc_format "POUs\MAIN.TcPOU"
```

The annotated profiles are the complete option references. Tests check their
option coverage and annotations against the built-in defaults. The sections
below explain the layouts and interactions in more detail.

## Profile examples

All three profiles use four-space indentation, hanging call alignment, and
`always` initializer wrapping. They retain up to one manually inserted blank
line between ordinary statements.

The examples below format the **same code** with each profile. Compare the variable declarations, assignments, blank lines, and closing delimiters.

### Example 1: Less whitespace

**Compact blocks, single spaces, closing delimiters after the last item.**

This profile follows the repository's Roslyn-inspired C# style where it maps to
Structured Text: four-space block and continuation indentation, single spaces
around operators, and no column padding. Declaration and return-type colons
have one space on each side (`value : BOOL`), including before an enum's opening
parenthesis (`TYPE E_Mode : (`). CASE labels stay compact (`1:`). C# settings
are not read by the Structured Text formatter; this profile selects its own
equivalent options. Multiline assignments align continuations beneath an
expression that starts beside `:=`; expressions starting on the next line use
one continuation indent. All profiles separate multiline assignments from
surrounding statements with one blank line, subject to block-boundary rules.

[Use this profile](../examples/less-whitespace.editorconfig)

```iecst
VAR
    i : INT := 0;
    targetPosition : LREAL := 100;
END_VAR

positions := [
    0,
    100];
IF ready THEN
    FOR i := 0 TO 1 DO
        _axis.Move(Position := positions[i],
                   Velocity := 20);
        moving := TRUE;
        requestedPosition := targetPosition;
    END_FOR
ELSE
    _axis.Stop();
END_IF
```

### Example 2: More whitespace

**Blank lines around blocks and after multiline calls, aligned columns, closing delimiters on their own line.**

[Use this profile](../examples/more-whitespace.editorconfig)

```iecst
VAR
    i              : INT   := 0;
    targetPosition : LREAL := 100;
END_VAR

positions := [
    0,
    100
];

IF ready THEN

    FOR i := 0 TO 1 DO

        _axis.Move(Position := positions[i],
                   Velocity := 20
        );

        moving            := TRUE;
        requestedPosition := targetPosition;

    END_FOR

ELSE
    _axis.Stop();

END_IF
```

### Example 3: More whitespace without assignment alignment

**The same blank lines as Example 2, with single spaces around declaration colons and assignment operators.**

[Use this profile](../examples/more-whitespace-without-assignment-alignment.editorconfig)

```iecst
VAR
    i : INT := 0;
    targetPosition : LREAL := 100;
END_VAR

positions := [
    0,
    100
];

IF ready THEN

    FOR i := 0 TO 1 DO

        _axis.Move(Position := positions[i],
                   Velocity := 20
        );

        moving := TRUE;
        requestedPosition := targetPosition;

    END_FOR

ELSE
    _axis.Stop();

END_IF
```

## Blank-line behavior

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

## Blank lines around comments

Use one setting for standalone `//` and `(* ... *)` comment groups:

```ini
tc_format_blank_line_before_comment = true
tc_format_blank_line_after_comment = false
```

`true` adds one blank line before a comment group, `false` removes blank lines
at that boundary, and `preserve` (the default) keeps the existing spacing.
The more-whitespace profiles use `true`; the less-whitespace profile uses `false`.

`tc_format_blank_line_after_comment` controls the boundary between a standalone
comment group and the following code. Use `false` to remove blank lines there,
`true` to require one, or `preserve` (the default) to retain existing spacing.
All supplied profiles use `false` to keep comments with the code they describe.
If the following keyword requests a blank line, the usual precedence applies:
`false` wins over `true`. This setting does not affect trailing or inline comments,
gaps between comment-only lines, or text inside multiline comments.

```iecst
Prepare();

// Start the next operation.
(* Both comment styles stay together. *)
Run();
```

Consecutive standalone comments form one group, including mixed comment styles
and multiline block comments. The setting does not insert gaps between those
comments, change text inside them, add a leading blank line at the start of a
file, or separate trailing and inline comments from their code. Existing gaps
between comment-only lines are preserved, subject to the global blank-line limit.

Existing block-boundary rules still take precedence: for example, no blank
line is inserted immediately after `VAR`, `METHOD`, or `ELSE`, or after `THEN`
when its after-header setting is `false`. The maximum consecutive blank-line
count also applies; setting it to `0` suppresses all blank lines.

## Alignment preferences

The variant without assignment alignment changes only these settings from the
more-whitespace profile:

```ini
tc_format_align_assignments = false
tc_format_align_declaration_initializers = false
tc_format_align_named_inputs = false
tc_format_align_named_outputs = false
tc_format_align_declarations = false
```

It retains blank lines and hanging argument indentation while avoiding padding
before declaration `:`, assignment `:=`, and named parameter `:=` / `=>`
operators. Disabling declaration alignment also disables column padding around
`AT` declarations. Trailing-comment alignment remains enabled; disable its
separate setting if you also want to remove that padding.
For example:

```iecst
IF ready THEN
    result := TRUE;
    requestedPosition := targetPosition;

END_IF
```

`tc_format_align_declarations` controls variable column alignment, including
directly addressed variables. When it is `false`, declarations use ordinary
spacing, such as `input AT %I* : BOOL;`. When it is `true`, declaration colons
align and `tc_format_align_addresses = true` also aligns the `AT` keywords.
Set `tc_format_align_addresses = false` to align colons without aligning `AT`.

Comment-only `//` lines follow their surrounding block's indentation. The gap
between code and a trailing `//` is controlled by
`tc_format_spaces_before_end_of_line_comment` (default `1`). Comment alignment
can add padding to this gap; disabling alignment retains the configured gap.
For example, to keep two spaces before trailing comments without aligning them:

```ini
tc_format_spaces_before_end_of_line_comment = 2
tc_format_align_end_of_line_comments = false
```

## Multiline assignments

Assignment continuations align beneath the first right-hand expression by
default. This is independent of column alignment between separate statements:

```iecst
equipmentModulesAreStopped := NOT _clampingBeam.Busy AND
                              NOT _runner.Busy AND
                              NOT _backgauge.Busy;
```

When the right-hand expression starts on the next line, it uses
`tc_format_continuation_indent_size` instead:

```iecst
equipmentModulesAreStopped :=
    NOT _clampingBeam.Busy AND
    NOT _runner.Busy AND
    NOT _backgauge.Busy;
```

Set `tc_format_align_assignment_continuations = false` to use ordinary
continuation indentation for both layouts. Calls and initializers retain their
delimiter layout settings. Declaration initializers and named arguments are
excluded from this assignment alignment rule.

All three example profiles also select:

```ini
tc_format_blank_line_before_multiline_assignment = true
tc_format_blank_line_after_multiline_assignment = true
```

Each option accepts `true`, `false`, or `preserve`, with a built-in default of
`preserve`. The rules use the final formatted line count, including assignments
expanded by wrapping. They add no separators between single-line assignments;
existing blank lines between those statements remain available for intentional
grouping. Continuation lines stay together, including intervening comments.
Block and comment boundary removal policies take precedence, and the global
maximum blank-line count still applies. No leading or trailing separator is
added at a code region's edges. These rules apply to standalone assignments,
including `REF=` and `?=`, rather than declaration initializers or named arguments.

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

## Spaces before parentheses after keywords

By default, control-flow keywords and word operators have one space before an
opening parenthesis: `IF (first) AND (second) OR (third) THEN`.
The same setting covers `IF`, `ELSIF`, `WHILE`, `UNTIL`, `CASE`, `AND`, `AND_THEN`,
`OR`, `OR_ELSE`, `XOR`, `NOT`, and `MOD`:

```ini
tc_format_space_before_parentheses_after_keywords = true
```

Set it to `false` for `IF(first) AND(second) OR(third) THEN`. It applies regardless
of keyword case and is independent of inside-parenthesis and binary-operator
spacing. Function calls such as `Check(value)` and `SIZEOF(value)` stay compact.
All supplied profiles enable this setting.

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

Use `always` to put every entry on a continuation line. With closing-bracket
placement set to `own_line` (or the wrapping default `preserve`), this produces
the following layout and avoids a large alignment offset after a long declaration:

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

Nested arrays and structures indent from their enclosing block. For example,
`always` initializer wrapping with `same_line` closing delimiters produces:

```iecst
beam : FB_Beam := (
    Name := 'Upper Bending Beam',
    WedgeAxisNames := [
        'Wedge Axis 1',
        'Wedge Axis 2'],
    HookAxisNames := [
        'Hook Axis 1',
        'Hook Axis 2']);
```

Each nested block uses the configured continuation indentation. With `own_line`,
each closing delimiter aligns with the line introducing its block. Existing
nested blocks also receive this indentation when wrapping is `preserve`.

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
