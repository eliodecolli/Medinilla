namespace Medinilla.Infrastructure.Tokenizer.Interfaces;

public interface IToken
{
    TokenType Type { get; }

    string Value { get; }
}
