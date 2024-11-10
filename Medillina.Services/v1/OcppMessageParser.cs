using Medinilla.DataTypes.WAMP;
using Medinilla.Infrastructure.Tokenizer.Interfaces;
using Medinilla.Infrastructure.Tokenizer;
using Medinilla.Services.Interfaces;

namespace Medinilla.Services.v1;

public sealed class OcppMessageParser(ITokenizer tokenizer) : IOcppMessageParser
{
    private List<IToken> _tokens = new();

    public void LoadRaw(string input)
    {
        _tokens = tokenizer.Tokenize(input).ToList();
    }

    private void AssertLoadFirst()
    {
        if (_tokens.Count == 0)
        {
            throw new Exception("Invalid Operation: Load the input first before using the parser.");
        }
    }

    public string? TryExtractMessageId()
    {
        AssertLoadFirst();

        return _tokens[1].Value;
    }

    public OcppJMessageType GetMessageType()
    {
        AssertLoadFirst();

        if (_tokens[0].Type == TokenType.Integer)
        {
            return (OcppJMessageType)int.Parse(_tokens[0].Value);
        }

        return default;
    }

    public OcppCallRequest ParseCall()
    {
        AssertLoadFirst();

        return new OcppCallRequest(_tokens[1].Value, _tokens[2].Value, _tokens[3].Value);
    }

    public OcppCallError ParseError()
    {
        AssertLoadFirst();

        return new OcppCallError(_tokens[1].Value, _tokens[2].Value, _tokens[3].Value, _tokens[4].Value);
    }

    public OcppCallResult ParseResult()
    {
        AssertLoadFirst();

        return new OcppCallResult(_tokens[1].Value, _tokens[2].Value);
    }
}
