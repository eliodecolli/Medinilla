using Medinilla.Infrastructure.Tokenizer.Interfaces;

namespace Medinilla.Infrastructure.Tokenizer;

public sealed class Token : IToken
{
    private readonly TokenType type;
    private readonly string value;

    public TokenType Type => type;

    public string Value => value;

    public Token(TokenType type, string value)
    {
        this.type = type;
        this.value = value;
    }
}
