using Medinilla.Infrastructure.Tokenizer.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Medinilla.Infrastructure.Tokenizer;

public sealed class OcppMessageTokenizerJson : ITokenizer
{
    public IEnumerable<IToken> Tokenize(string input)
    {
        var document = JsonDocument.Parse(input);
        var tokens = new List<IToken>();

        var iter = document.RootElement.EnumerateArray();
        while(iter.MoveNext())
        {
            switch(iter.Current.ValueKind)
            {
                case JsonValueKind.Object:
                    tokens.Add(new Token(TokenType.Json, iter.Current.ToString()));
                    break;
                case JsonValueKind.String:
                    tokens.Add(new Token(TokenType.String, iter.Current.GetString() ?? ""));
                    break;
                case JsonValueKind.Number:
                    tokens.Add(new Token(TokenType.Integer, iter.Current.ToString()));
                    break;
                default:
                    tokens.Add(new Token(TokenType.Unknown, iter.Current.ToString()));
                    break;
            }
        }
        return tokens;
    }
}
