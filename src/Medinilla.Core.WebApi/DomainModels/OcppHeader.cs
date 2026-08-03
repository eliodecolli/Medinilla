using Medinilla.Core.SharedContracts.Comms;

namespace Medinilla.Core.WebApi.Services.Domain;

public sealed record class OcppHeader(CommsMessageType Type, string MessageId);
