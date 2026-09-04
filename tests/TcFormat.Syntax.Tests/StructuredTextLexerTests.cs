using TcFormat.Syntax;

using Xunit;

namespace TcFormat.Syntax.Tests;

public sealed class StructuredTextLexerTests
{
    [Fact]
    public void TokenizationIsLosslessForRepresentativeTwinCatSource()
    {
        const string source =
            """
            {attribute 'qualified_only'}
            FUNCTION_BLOCK FB_Example EXTENDS FB_Base
            VAR_INPUT
                bEnable AT %I* : BOOL := TRUE;
            END_VAR

            (* Outer comment (* nested comment *) still outer *)
            IF bEnable AND (nCount >= 16#10) THEN
                fbAxis(
                    bExecute := TRUE,
                    bDone => bDone);
                sText := 'It$'s safe'; // Keep me
            END_IF
            """;

        var result = StructuredTextLexer.Lex(source);

        Assert.True(result.IsValid);
        Assert.Equal(source, result.ReconstructSource());
        Assert.Contains(result.Tokens, token => token is { Kind: SyntaxKind.DirectAddress, Text: "%I*" });
        Assert.Contains(result.Tokens, token => token is { Kind: SyntaxKind.Operator, Text: ":=" });
        Assert.Contains(result.Tokens, token => token is { Kind: SyntaxKind.Operator, Text: "=>" });
        Assert.Contains(result.Tokens, token => token.Kind == SyntaxKind.Pragma);
    }

    [Fact]
    public void LexerTracksCrLfAndLfLocations()
    {
        const string source = "VAR\r\n    value : INT;\nEND_VAR";

        var result = StructuredTextLexer.Lex(source);
        var value = Assert.Single(result.Tokens, token => token.Text == "value");
        var endVar = Assert.Single(result.Tokens, token => token.Text == "END_VAR");

        Assert.Equal((2, 5), (value.Line, value.Column));
        Assert.Equal((3, 1), (endVar.Line, endVar.Column));
        Assert.Equal(source, result.ReconstructSource());
    }

    [Theory]
    [InlineData("(* not closed", "Unterminated block comment.")]
    [InlineData("'not closed", "Unterminated string literal.")]
    [InlineData("{attribute 'not closed'", "Unterminated pragma.")]
    public void UnterminatedConstructProducesDiagnostic(string source, string message)
    {
        var result = StructuredTextLexer.Lex(source);

        Assert.False(result.IsValid);
        Assert.Equal(source, result.ReconstructSource());
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message == message);
    }

    [Fact]
    public void SemanticFingerprintIgnoresLayoutAndKeywordCaseOnly()
    {
        var compact = StructuredTextLexer.Lex("if x:=1 then\nEND_IF");
        var spaced = StructuredTextLexer.Lex("IF   x := 1   THEN\r\nend_if");
        var changed = StructuredTextLexer.Lex("IF x := 2 THEN\nEND_IF");

        Assert.Equal(
            SemanticTokenFingerprint.Create(compact.Tokens),
            SemanticTokenFingerprint.Create(spaced.Tokens));
        Assert.NotEqual(
            SemanticTokenFingerprint.Create(compact.Tokens),
            SemanticTokenFingerprint.Create(changed.Tokens));
    }

    [Fact]
    public void RecognizesReferenceAssignmentAndUnspacedDirectAddressColon()
    {
        const string source = "reference REF= source; input AT %IX0.0: BOOL;";

        var result = StructuredTextLexer.Lex(source);

        Assert.Contains(result.Tokens, token => token is { Kind: SyntaxKind.Operator, Text: "REF=" });
        Assert.Contains(result.Tokens, token => token is { Kind: SyntaxKind.DirectAddress, Text: "%IX0.0" });
        Assert.Contains(result.Tokens, token => token is { Kind: SyntaxKind.Operator, Text: ":" });
    }
}

