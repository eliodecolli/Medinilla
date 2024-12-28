using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Infrastructure.Interfaces;

public interface IOcppMessageParser
{
    void LoadRaw(string input);

    OcppCallResult ParseResult();

    OcppCallError ParseError();

    OcppCallRequest ParseCall();

    OcppJMessageType GetMessageType();

    string? TryExtractMessageId();
}
