using System.Net;
using Google.Protobuf;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.Webhooks.Service.Configuration;
using Medinilla.Webhooks.Service.Delivery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medinilla.Webhooks.Service.Tests;

public class WebhookDispatcherShould
{
    [Fact]
    public async Task DeliverMatchingSubscriptionsWithTheOriginalBodyAndReportMixedOutcomes()
    {
        var options = Options(
            Subscription("ok", "http://localhost/ok", "ok", WebhookEventTypes.ChargerBootup),
            Subscription("bad", "http://localhost/bad", "bad", WebhookEventTypes.ChargerBootup),
            Subscription("filtered-out", "http://localhost/other", "other", WebhookEventTypes.ConnectorStatus));
        var handler = new RecordingHandler(request =>
            request.RequestUri!.AbsolutePath == "/ok" ? HttpStatusCode.OK : HttpStatusCode.InternalServerError);
        var dispatcher = CreateDispatcher(options, handler);

        var httpEvent = new MedinillaHttpEvent
        {
            EventId = Guid.NewGuid().ToString("D"),
            EventType = WebhookEventTypes.ChargerBootup,
            EventTimestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
            EventData = ByteString.CopyFromUtf8("payload"),
        };
        var body = httpEvent.ToByteArray();

        var result = await dispatcher.DispatchAsync(httpEvent, body, CancellationToken.None);

        Assert.Equal(2, result.Matched);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.True(result.HasFailures);
        Assert.Equal(2, handler.Requests.Count);

        var ok = Assert.Single(handler.Requests, request => request.Url.AbsolutePath == "/ok");
        Assert.Equal("application/protobuf", ok.ContentType);
        Assert.Equal(httpEvent.EventId, ok.EventId);
        Assert.Equal(WebhookEventTypes.ChargerBootup, ok.EventType);
        Assert.Equal(body, ok.Body);

        // The signature covers the timestamp and the exact bytes on the wire.
        var expected = WebhookSigner.ComputeSignature("ok-secret", ok.Timestamp!, ok.Body);
        Assert.Equal(expected, ok.Signature);
    }

    [Fact]
    public async Task AcknowledgeableWhenNoSubscriptionMatches()
    {
        var dispatcher = CreateDispatcher(
            Options(Subscription(
                "bootup-only",
                "http://localhost/ok",
                "ok",
                WebhookEventTypes.ChargerBootup)),
            new RecordingHandler(_ => HttpStatusCode.OK));

        var httpEvent = new MedinillaHttpEvent
        {
            EventId = Guid.NewGuid().ToString("D"),
            EventType = WebhookEventTypes.ConnectorStatus,
        };

        var result = await dispatcher.DispatchAsync(
            httpEvent,
            httpEvent.ToByteArray(),
            CancellationToken.None);

        Assert.Equal(0, result.Matched);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void SkipDisabledSubscriptionsWithoutResolvingTheirSecret()
    {
        var disabled = Subscription("disabled", "http://localhost/disabled", "missing", null);
        disabled.Enabled = false;

        var registry = CreateRegistry(Options(disabled));

        Assert.Empty(registry.Match(WebhookEventTypes.ChargerBootup));
    }

    [Fact]
    public void RejectAnInvalidSubscriptionUrl()
    {
        var invalid = Subscription("invalid", "not-a-url", "ok", null);

        Assert.Throws<InvalidOperationException>(() => CreateRegistry(Options(invalid)));
    }

    private static WebhookDispatcher CreateDispatcher(
        WebhookOptions options,
        RecordingHandler handler)
    {
        var registry = CreateRegistry(options);
        return new WebhookDispatcher(
            new FakeHttpClientFactory(new HttpClient(handler)),
            registry,
            options,
            TimeProvider.System,
            NullLogger<WebhookDispatcher>.Instance);
    }

    private static WebhookSubscriptionRegistry CreateRegistry(WebhookOptions options)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Webhooks:Secrets:ok"] = "ok-secret",
                ["Webhooks:Secrets:bad"] = "bad-secret",
                ["Webhooks:Secrets:other"] = "other-secret",
            })
            .Build();

        return new WebhookSubscriptionRegistry(
            options,
            new WebhookSecretResolver(configuration),
            NullLogger<WebhookSubscriptionRegistry>.Instance);
    }

    private static WebhookOptions Options(params WebhookSubscriptionOptions[] subscriptions) => new()
    {
        MaxConcurrency = 2,
        Subscriptions = [.. subscriptions],
    };

    private static WebhookSubscriptionOptions Subscription(
        string id,
        string url,
        string secretRef,
        string? eventType) => new()
    {
        Id = id,
        Url = url,
        SigningSecretRef = secretRef,
        EventTypes = eventType is null ? null : [eventType],
    };

    private sealed record CapturedRequest(
        Uri Url,
        byte[] Body,
        string? ContentType,
        string? EventId,
        string? EventType,
        string? Timestamp,
        string? Signature);

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpStatusCode> status) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);

            Requests.Add(new CapturedRequest(
                request.RequestUri!,
                body,
                request.Content.Headers.ContentType?.MediaType,
                request.Headers.TryGetValues(WebhookHeaders.EventId, out var eventId) ? eventId.Single() : null,
                request.Headers.TryGetValues(WebhookHeaders.EventType, out var eventType) ? eventType.Single() : null,
                request.Headers.TryGetValues(WebhookHeaders.Timestamp, out var timestamp) ? timestamp.Single() : null,
                request.Headers.TryGetValues(WebhookHeaders.Signature, out var signature) ? signature.Single() : null));

            return new HttpResponseMessage(status(request));
        }
    }

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
