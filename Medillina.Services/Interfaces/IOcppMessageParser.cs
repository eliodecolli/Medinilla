using Medinilla.DataTypes.WAMP;

namespace Medinilla.Services.Interfaces;

public interface IOcppMessageParser
{
    void LoadRaw(string input);

    OcppCallResult ParseResult();

    OcppCallError ParseError();

    OcppCallRequest ParseCall();

    OcppJMessageType GetMessageType();

    string? TryExtractMessageId();
}
