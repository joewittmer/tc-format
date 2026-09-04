namespace TcFormat.Syntax;

public static class KeywordFacts
{
    private static readonly HashSet<string> Keywords = new(
        [
            "ACTION", "AND", "AND_THEN", "ANY", "ANY_BIT", "ANY_DATE", "ANY_DERIVED",
            "ANY_ELEMENTARY", "ANY_INT", "ANY_MAGNITUDE", "ANY_NUM", "ANY_REAL", "ARRAY",
            "AT", "BIT", "BOOL", "BYTE", "BY", "CASE", "CHAR", "CLASS", "CONSTANT", "CONTINUE",
            "DATE", "DATE_AND_TIME", "DINT", "DT", "DWORD",
            "DO", "ELSE", "ELSIF", "END_ACTION", "END_CASE", "END_CLASS",
            "END_CONFIGURATION", "END_FOR", "END_FUNCTION", "END_FUNCTION_BLOCK",
            "END_IF", "END_INTERFACE", "END_METHOD", "END_NAMESPACE", "END_PROGRAM",
            "END_PROPERTY", "END_REPEAT", "END_RESOURCE", "END_STEP", "END_STRUCT",
            "END_TRANSITION", "END_TYPE", "END_UNION", "END_VAR", "END_WHILE", "EXIT",
            "EXTENDS", "EXTERNAL", "FALSE", "FINAL", "FOR", "FROM", "FUNCTION",
            "FUNCTION_BLOCK", "F_EDGE", "IF", "IMPLEMENTS", "INT", "INTERFACE", "INTERNAL",
            "LDATE", "LDATE_AND_TIME", "LDT", "LINT", "LREAL", "LTIME", "LWORD", "METHOD",
            "MOD", "NAMESPACE", "NON_RETAIN", "NOT", "OF", "OR", "OR_ELSE", "OVERRIDE",
            "PERSISTENT", "POINTER", "PRIVATE", "PROGRAM", "PROPERTY", "PROTECTED", "PUBLIC",
            "READ_ONLY", "REAL", "REFERENCE", "RETAIN",
            "REPEAT", "RESOURCE", "RETURN", "R_EDGE", "STEP", "STRUCT", "SUPER",
            "SINT", "STRING", "THIS", "THEN", "TIME", "TIME_OF_DAY", "TO", "TOD", "TRANSITION",
            "TRUE", "TYPE", "UDINT", "UINT", "ULINT", "UNION", "UNTIL", "USINT", "VAR",
            "VAR_ACCESS", "VAR_CONFIG", "VAR_EXTERNAL", "VAR_GLOBAL", "VAR_IN_OUT",
            "VAR_INPUT", "VAR_INST", "VAR_OUTPUT", "VAR_STAT", "VAR_TEMP", "WHILE",
            "VOID", "WCHAR", "WHILE", "WITH", "WORD", "WRITE_ONLY", "WSTRING", "XOR",
            "__CATCH", "__ENDTRY", "__FINALLY", "__TRY"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static bool IsKeyword(string text) => Keywords.Contains(text);
}

