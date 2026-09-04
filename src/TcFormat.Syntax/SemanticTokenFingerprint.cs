using System.Security.Cryptography;
using System.Text;

namespace TcFormat.Syntax;

public static class SemanticTokenFingerprint
{
    public static string Create(IEnumerable<SyntaxToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var canonical = new StringBuilder();
        foreach (var token in tokens)
        {
            if (token.Kind is SyntaxKind.Whitespace or SyntaxKind.NewLine)
            {
                continue;
            }

            var text = token.Kind == SyntaxKind.Keyword
                ? token.Text.ToUpperInvariant()
                : NormalizeCommentNewLines(token);
            canonical.Append((int)token.Kind).Append(':').Append(text.Length).Append(':').Append(text).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string NormalizeCommentNewLines(SyntaxToken token) =>
        token.Kind is SyntaxKind.LineComment or SyntaxKind.BlockComment
            ? token.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            : token.Text;
}

