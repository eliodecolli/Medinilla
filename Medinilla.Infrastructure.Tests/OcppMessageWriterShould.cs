using System.Text;

namespace Medinilla.Infrastructure.Tests;


public class OcppMessageWriterShould
{
    public const string RawMessage =
        "[2,\"123-abc-456-def\",\"BootNotification\",{\"reason\": \"PowerUp\",\r\n\"chargingStation\": { \"model\": \"Test Model\", \"vendor\": \"Test Vendor\" }}]";

    [Fact]
    public void WriteAWholeDamnMessage()
    {
        var writer = new OcppMessageWriter();

        writer.WriteInt(2);
        writer.WriteString("123-abc-456-def");
        writer.WriteString("BootNotification");
        writer.WriteJson("{\"reason\": \"PowerUp\",\r\n\"chargingStation\": { \"model\": \"Test Model\", \"vendor\": \"Test Vendor\" }}");

        var result = writer.Serialize();

        var match = Encoding.Unicode.GetBytes(RawMessage);

        var message = writer.GetMessage() + "\r\n" + RawMessage;

        Assert.Equal(match, result);
    }
}
