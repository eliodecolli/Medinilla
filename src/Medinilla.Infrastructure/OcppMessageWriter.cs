using Medinilla.Infrastructure.Tokenizer;
using System.Text;

namespace Medinilla.Infrastructure;

public class OcppMessageWriter
{
    private StringBuilder _builder;

    private bool _open, _close;

    public OcppMessageWriter()
    {
        _builder = new StringBuilder();
    }

    public string GetMessage()
    {
        return _builder.ToString();
    }

    public byte[] Serialize()
    {
        if (!_close)
        {
            _builder.Append("]");
            _close = true;
        }

        return GetUnicodeBytes(_builder.ToString());
    }

    private byte[] GetUnicodeBytes(string value)
    {
        return Encoding.Unicode.GetBytes(value);
    }


    private void WriteQuotationMark() => _builder.Append("\"");

    private void WriteColon() => _builder.Append(",");


    private void WriteToken(Token token)
    {
        if (!_open)
        {
            _builder.Append("[");
            _open = true;
        }
        else
        {
            WriteColon();
        }

        switch (token.Type)
        {
            case TokenType.String:
                WriteQuotationMark();
                _builder.Append(token.Value);
                WriteQuotationMark();
                break;

            // we do not append quotation marks for JSON or Int32 values
            case TokenType.Integer:
                _builder.Append(token.Value);
                break;

            case TokenType.Json:
                _builder.Append(token.Value);
                break;
        }
    }

    public void WriteInt(int value)
    {
        WriteToken(new Token(TokenType.Integer, value.ToString()));
    }

    public void WriteString(string value)
    {
        WriteToken(new Token(TokenType.String, value));
    }

    public void WriteJson(string json)
    {
        WriteToken(new Token(TokenType.Json, json));
    }
}
