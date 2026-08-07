---
title: Configuration
description: Config keys and dependency injection wiring.
sidebar_position: 11
---

# Configuration

## WebApi

`Medinilla.Core.WebApi/appsettings.json`

```json
{
  "Comms": {
    "RequestQueue": "medinilla.core.request"
  },
  "General": {
    "MessageQueueTTL": 5
  }
}
```

| Key | Read by | Missing |
| --- | --- | --- |
| `Comms:RequestQueue` | `WebSocketDigestionService.Consume` | Throws `InvalidOperationException` |
| `General:MessageQueueTTL` | `MessageQueueFactory` | Throws `InvalidOperationException` |

`MessageQueueTTL` must parse as a `uint`.

There is no response-queue key. The response queue is derived from `IInstanceIdentifier`.

## CSMS

`Medinilla.Core.Service/settings.json`, embedded as a resource.

```json
{
  "RequestQueue": "medinilla.core.request",
  "ConnectionStrings": {
    "MedinillaCore": "..."
  }
}
```

Bound by `Types/CommunicationSettings.cs`:

```csharp
public sealed class CommunicationSettings
{
    public string RequestQueue { get; private set; }
    public static CommunicationSettings FromSettingsFile(string settingsFile);
}
```

A missing `RequestQueue` throws `JsonException`.

## Redis

`Medinilla.RealTime/appsettings.json`, embedded as a resource. See [Transport](./transport.md).

## DI — WebApi

`Medinilla.Core.WebApi/Program.cs`

```csharp
builder.Services.AddRealTimeServices();
builder.Services.AddWebSocketRoutingTable();
builder.Services.AddSubscriptionReceiver();

builder.Services.AddSingleton<IInstanceIdentifier, InstanceIdentifier>();
builder.Services.AddSingleton<IMessageQueueFactory, MessageQueueFactory>();
builder.Services.AddHostedService<SubscriptionReceiverHostedService>();

builder.Services.AddScoped<IWSDigestionServiceCollection, WSDigestionServiceCollection>();
builder.Services.AddScoped<IBasicWebSocketDigestionService, WebSocketDigestionService>();
```

## DI — CSMS

`Medinilla.Core.Service/Program.cs`

```csharp
builder.Services.AddRealTimeServices();
builder.Services.AddWebSocketRoutingTable();

builder.Services.AddSingleton(CommunicationSettings.FromSettingsFile("settings.json"));
builder.Services.AddScoped<IOcppRequestDispatcher, OcppRequestDispatcher>();
builder.Services.AddSingleton<IInterfaceCommunication, CoreInterfaceCommunication>();
builder.Services.AddHostedService<InboundWorker>();
```

## Extension methods

`Medinilla.RealTime/ServiceCollectionExtension.cs`

```csharp
public static IServiceCollection AddRealTimeServices(this IServiceCollection services);
public static IServiceCollection AddWebSocketRoutingTable(this IServiceCollection services);
public static IServiceCollection AddSubscriptionReceiver(this IServiceCollection services);
```

`AddRealTimeServices()` registers the multiplexers and must be called before the other two.

## Scaling

| Change | Effect |
| --- | --- |
| Add a WebApi replica | New instance ID, new response queue, no config change |
| Add a CSMS replica | Competes for `medinilla.core.request`; each message goes to one consumer |
| Restart a WebApi replica | New instance ID; old routing entries expire within 60s |
