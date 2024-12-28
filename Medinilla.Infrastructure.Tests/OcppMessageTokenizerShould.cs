using Medinilla.Infrastructure.Tokenizer;
using System.Text.Json;

namespace Medinilla.Infrastructure.Tests;


public class OcppMessageTokenizerShould
{
    public const string RawMessage =
        "[2     ,  \"123-abc-456-def\" , \"BootNotification\"  , {\"reason\": \"PowerUp\",\r\n\"chargingStation\": { \"model\": \"Test Model\", \"vendor\": \"Test Vendor\" }}]";

    public const string RawMessageEmptyJson =
        "[2     ,  \"123-abc-456-def\" , \"BootNotification\"  , {}]";

    public const string RawMessageInvalid =
        "[2     ,  \"123-abc-456-def\" , \"BootNotification\"  , {]";

    public const string RawMessageError =
        "[4,\"46fec632-7413-4a12-935c-540337813301\",\"SecurityError\",\"\",{}]";

    class ChargingStation
    {
        public string Model { get; set; }

        public string Vendor { get; set; }
    }

    class BootNotification
    {
        public string Reason { get; set; }

        public ChargingStation ChargingStation { get; set; }
    }

    [Fact]
    public void GetTokensSuccessfullyFromErrorMessage()
    {
        var tokenizer = new OcppMessageTokenizer();

        var tokens = tokenizer.Tokenize(RawMessageError).ToList();
        Assert.Equal(5, tokens.Count);

        var numberToken = tokens[0];
        Assert.Equal(TokenType.Integer, numberToken.Type);
        Assert.True(int.TryParse(numberToken.Value, out int _));
        Assert.Equal("4", numberToken.Value);

        var uid = tokens[1];
        Assert.Equal(TokenType.String, uid.Type);
        Assert.Equal("46fec632-7413-4a12-935c-540337813301", uid.Value);

        var msgCode = tokens[2];
        Assert.Equal(TokenType.String, msgCode.Type);
        Assert.Equal("SecurityError", msgCode.Value);

        var details = tokens[3];
        Assert.Equal(TokenType.String, details.Type);
        Assert.Equal("", details.Value);

        var json = tokens[4];
        Assert.Equal(TokenType.Json, json.Type);
        Assert.Equal("{}", json.Value);
    }

    [Fact]
    public void GetTokensSuccessfully()
    {
        var tokenizer = new OcppMessageTokenizer();

        var tokens = tokenizer.Tokenize(RawMessage).ToList();
        Assert.Equal(4, tokens.Count);

        var numberToken = tokens[0];
        Assert.Equal(TokenType.Integer, numberToken.Type);
        Assert.True(int.TryParse(numberToken.Value, out int _));

        var stringToken1 = tokens[1];
        Assert.Equal(TokenType.String, stringToken1.Type);
        Assert.Equal("123-abc-456-def", stringToken1.Value);

        var stringToken2 = tokens[2];
        Assert.Equal(TokenType.String, stringToken2.Type);
        Assert.Equal("BootNotification", stringToken2.Value);

        var jsonToken = tokens[3];
        Assert.Equal(TokenType.Json, jsonToken.Type);

        var parsedJson = JsonSerializer.Deserialize<BootNotification>(jsonToken.Value, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
        Assert.NotNull(parsedJson);
        Assert.Equal("PowerUp", parsedJson.Reason);
        Assert.Equal("Test Model", parsedJson.ChargingStation.Model);
        Assert.Equal("Test Vendor", parsedJson.ChargingStation.Vendor);
    }

    [Fact]
    public void NotPanicWhenEmptyJsonIsFound()
    {
        var tokenizer = new OcppMessageTokenizer();

        var tokens = tokenizer.Tokenize(RawMessageEmptyJson).ToList();
        Assert.Equal(4, tokens.Count);

        var numberToken = tokens[0];
        Assert.Equal(TokenType.Integer, numberToken.Type);
        Assert.True(int.TryParse(numberToken.Value, out int _));

        var stringToken1 = tokens[1];
        Assert.Equal(TokenType.String, stringToken1.Type);
        Assert.Equal("123-abc-456-def", stringToken1.Value);

        var stringToken2 = tokens[2];
        Assert.Equal(TokenType.String, stringToken2.Type);
        Assert.Equal("BootNotification", stringToken2.Value);

        var jsonToken = tokens[3];
        Assert.Equal(TokenType.Json, jsonToken.Type);
        Assert.Equal("{}", jsonToken.Value);
    }

    [Fact]
    public void PanicWhenInvalidMessage()
    {
        var tokenizer = new OcppMessageTokenizer();
        Assert.Throws<Exception>(() => tokenizer.Tokenize(RawMessageInvalid));
    }
}
