namespace Medinilla.Infrastructure.Tokenizer.Interfaces;

public interface ITokenizer
{
    IEnumerable<IToken> Tokenize(string input);
}
