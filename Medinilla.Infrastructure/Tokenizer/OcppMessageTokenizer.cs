using Medinilla.Infrastructure.Tokenizer.Interfaces;

namespace Medinilla.Infrastructure.Tokenizer;

public sealed class OcppMessageTokenizer : ITokenizer
{
    private string TokenizeStringValue(string input, int startIndex)
    {
        var currentValue = "";
        for(int i = startIndex; i < input.Length; i++)
        {
            if (i == input.Length - 1)
            {
                throw new Exception($"Invalid Tokenization: Invalid end of input at {i}: {currentValue}.");
            }

            if (input[i] == '"')
            {
                // end of string
                return currentValue;
            }
            else
            {
                currentValue += input[i];
            }
        }

        throw new Exception("Invalid Tokenization: Expected '\"' but nothing was found.");
    }

    private string TokenizeInteger(string input, int startIndex)
    {
        var currentValue = "";

        for (int i = startIndex; i < input.Length; i++)
        {
            var c = input[i];

            if (i == input.Length - 1)
            {
                throw new Exception($"Invalid Tokenization: Invalid end of input at {i}: {currentValue}.");
            }

            if (c == ',' || c == ']')
            {
                // validate integer
                if(int.TryParse(currentValue, out int _))
                {
                    // everything is ok
                    return currentValue;
                }
                else
                {
                    throw new Exception($"Invalid Tokenization: Couldn't parse integer from value: '{currentValue}'.");
                }
            }
            else
            {
                if (char.IsWhiteSpace(c) || c == '\r' || c == '\n')
                {
                    // ignore white spaces and new lines
                    continue;
                }

                if (!char.IsDigit(c))
                {
                    throw new Exception($"Invalid Tokenization: Expected number got: '{c}'.");
                }
                currentValue += c;
            }
        }

        throw new Exception("Invalid Tokenization: Expected ',' or ']' but nothing was found.");
    }

    private string TokenizeJson(string input, int startIndex)
    {
        var currentValue = "";
        var openBrackets = 0;
        var closedBrackets = 0;

        for (int i = startIndex; i < input.Length; i++)
        {
            var c = input[i];

            if(openBrackets > 0 && closedBrackets > 0 && openBrackets == closedBrackets)
            {
                return currentValue;
            }

            if(c == '{')
            {
                openBrackets++;
            }

            if(c == '}')
            {
                closedBrackets++;
            }

            currentValue += c;
        }

        throw new Exception($"Invalid Tokenization: Expected JSON but got: {currentValue}.");
    }
    
    public IEnumerable<IToken> Tokenize(string input)
    {
        var tokens = new List<IToken>();
        var idx = 0;

        while(idx  < input.Length)
        {
            var c = input[idx];
            var token = "";
            var tokenType = TokenType.Unknown;

            if(char.IsDigit(c))
            {
                token = TokenizeInteger(input, idx);
                tokenType = TokenType.Integer;
                idx += token.Length;
            } 
            else if(c == '"')
            {
                token = TokenizeStringValue(input, idx + 1);
                tokenType = TokenType.String;
                idx += token.Length + 2;
            }
            else if (c == '{')
            {
                token = TokenizeJson(input, idx);
                tokenType = TokenType.Json;
                idx += token.Length;
            }
            else
            {
                idx += 1;
            }

            if (!string.IsNullOrEmpty(token))
            {
                var createdToken = new Token(tokenType, token);
                tokens.Add(createdToken);
            }
            
        }

        return tokens;
    }
}
