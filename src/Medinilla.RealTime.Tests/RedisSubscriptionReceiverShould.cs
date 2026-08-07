using Google.Protobuf;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.RealTime.Redis;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;

namespace Medinilla.RealTime.Tests;

public class RedisSubscriptionReceiverShould
{
    private const string QUEUE = "medinilla.ws.deadbeef.response";
    private const string CLIENT_A = "CHARGER-A";
    private const string CLIENT_B = "CHARGER-B";

    private readonly Mock<IReceiver> _receiverMock = new();
    private readonly Mock<ILogger<RedisSubscriptionReceiver>> _loggerMock = new();

    // Feeds ReceiveAsync one payload at a time; blocks in between, like BLPOP does.
    private readonly ConcurrentQueue<byte[]> _queue = new();
    private readonly SemaphoreSlim _available = new(0);

    public RedisSubscriptionReceiverShould()
    {
        _receiverMock
            .Setup(r => r.ReceiveAsync(QUEUE, It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (_, ct) =>
            {
                await _available.WaitAsync(ct);
                _queue.TryDequeue(out var payload);
                return payload;
            });
    }

    private RedisSubscriptionReceiver CreateReceiver()
        => new(_receiverMock.Object, _loggerMock.Object);

    private void Push(byte[] raw)
    {
        _queue.Enqueue(raw);
        _available.Release();
    }

    private void Push(string clientId, string ocpp = "[3,\"m-1\",{}]")
        => Push(new QueuedMessageResponse
        {
            ClientIdentifier = clientId,
            Payload = new Comms
            {
                MessageType = CommsMessageType.OcppResponse,
                ClientIdentifier = clientId,
                Payload = ByteString.CopyFromUtf8(ocpp),
            },
        }.ToByteArray());

    private static async Task<bool> Settles(Task task, int timeoutMs = 2000)
        => await Task.WhenAny(task, Task.Delay(timeoutMs)) == task;

    [Fact]
    public async Task DeliverMessageToMatchingSubscriber()
    {
        var received = new TaskCompletionSource<QueuedMessageResponse>();

        await using var receiver = CreateReceiver();
        receiver.Subscribe(CLIENT_A, (response, _) =>
        {
            received.TrySetResult(response);
            return Task.CompletedTask;
        });
        receiver.Start(QUEUE);

        Push(CLIENT_A, "[3,\"boot-1\",{}]");

        Assert.True(await Settles(received.Task), "callback was never invoked");

        var delivered = await received.Task;
        Assert.Equal(CLIENT_A, delivered.ClientIdentifier);
        Assert.Equal("[3,\"boot-1\",{}]", delivered.Payload.Payload.ToStringUtf8());
    }

    [Fact]
    public async Task StartOnlyOnce()
    {
        await using var receiver = CreateReceiver();
        receiver.Start(QUEUE);
        receiver.Start("some-other-queue");

        var received = new TaskCompletionSource();
        receiver.Subscribe(CLIENT_A, (_, _) =>
        {
            received.TrySetResult();
            return Task.CompletedTask;
        });

        Push(CLIENT_A);

        Assert.True(await Settles(received.Task), "loop should still be draining the first queue");
        _receiverMock.Verify(r => r.ReceiveAsync("some-other-queue", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StopDeliveringAfterUnsubscribe()
    {
        var deliveries = 0;
        var first = new TaskCompletionSource();

        await using var receiver = CreateReceiver();
        receiver.Subscribe(CLIENT_A, (_, _) =>
        {
            Interlocked.Increment(ref deliveries);
            first.TrySetResult();
            return Task.CompletedTask;
        });
        receiver.Start(QUEUE);

        Push(CLIENT_A);
        Assert.True(await Settles(first.Task));

        receiver.Unsubscribe(CLIENT_A);
        Push(CLIENT_A);

        // Give the loop room to dispatch the second message if it wrongly still can.
        await Task.Delay(200);
        Assert.Equal(1, Volatile.Read(ref deliveries));
    }

    [Fact]
    public async Task DropMessagesWithNoSubscriber()
    {
        var deliveries = 0;
        var forB = new TaskCompletionSource();

        await using var receiver = CreateReceiver();
        receiver.Subscribe(CLIENT_B, (_, _) =>
        {
            Interlocked.Increment(ref deliveries);
            forB.TrySetResult();
            return Task.CompletedTask;
        });
        receiver.Start(QUEUE);

        // Unknown routing key first — must be dropped without killing the loop.
        Push(CLIENT_A);
        Push(CLIENT_B);

        Assert.True(await Settles(forB.Task), "loop stopped after an unroutable message");
        Assert.Equal(1, Volatile.Read(ref deliveries));
    }

    [Fact]
    public async Task SurviveMalformedPayload()
    {
        var delivered = new TaskCompletionSource();

        await using var receiver = CreateReceiver();
        receiver.Subscribe(CLIENT_A, (_, _) =>
        {
            delivered.TrySetResult();
            return Task.CompletedTask;
        });
        receiver.Start(QUEUE);

        Push([0xFF, 0xFF, 0xFF, 0xFF]);
        Push(CLIENT_A);

        Assert.True(await Settles(delivered.Task), "loop stopped after a malformed payload");
    }

    [Fact]
    public async Task SurviveThrowingSubscriber()
    {
        var second = new TaskCompletionSource();
        var calls = 0;

        await using var receiver = CreateReceiver();
        receiver.Subscribe(CLIENT_A, (_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("subscriber blew up");
            }

            second.TrySetResult();
            return Task.CompletedTask;
        });
        receiver.Start(QUEUE);

        Push(CLIENT_A);
        Push(CLIENT_A);

        Assert.True(await Settles(second.Task), "loop died with the throwing subscriber");
    }

    [Fact]
    public async Task SurviveSubscriberReturningFaultedTask()
    {
        var second = new TaskCompletionSource();
        var calls = 0;

        await using var receiver = CreateReceiver();
        receiver.Subscribe(CLIENT_A, (_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                return Task.FromException(new InvalidOperationException("async blow up"));
            }

            second.TrySetResult();
            return Task.CompletedTask;
        });
        receiver.Start(QUEUE);

        Push(CLIENT_A);
        Push(CLIENT_A);

        Assert.True(await Settles(second.Task), "loop died on a faulted subscriber task");
    }

    [Fact]
    public async Task CancelLoopOnDispose()
    {
        var receiver = CreateReceiver();
        receiver.Start(QUEUE);

        // Let the loop reach its blocking receive before tearing it down.
        await Task.Delay(50);

        Assert.True(await Settles(receiver.DisposeAsync().AsTask()), "DisposeAsync did not complete");

        // Idempotent: a second dispose is a no-op.
        await receiver.DisposeAsync();
    }
}
