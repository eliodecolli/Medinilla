using System.Text;
using Medinilla.Webhooks.Service.Delivery;

namespace Medinilla.Webhooks.Service.Tests;

public class WebhookSignerShould
{
    [Fact]
    public void SignTheTimestampAndExactBodyBytes()
    {
        var signature = WebhookSigner.ComputeSignature(
            "secret",
            "1700000000",
            Encoding.UTF8.GetBytes("hello"));

        Assert.Equal(
            "v1=47b1df0ab12338b2685470b0d2b37033add7c3b2bc8172f313e77413f1bb78c8",
            signature);
    }
}
