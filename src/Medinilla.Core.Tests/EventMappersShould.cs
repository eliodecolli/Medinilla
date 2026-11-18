using Medinilla.Core.Events;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.Core.Tests;

public class EventMappersShould
{
    [Fact]
    public void MapBootupPresenceAndNamesWithoutIo()
    {
        var payload = new BootupEventMapper().Map(
            "station-1",
            new BootNotificationRequest(
                BootReasonEnum.PowerUp,
                new ChargingStation
                {
                    VendorName = "Vendor",
                    Model = "Model",
                    SerialNumber = "serial-1",
                }));

        Assert.Equal("station-1", payload.ChargerId);
        Assert.Equal("PowerUp", payload.BootReason);
        Assert.Equal("Vendor", payload.VendorName);
        Assert.Equal("Model", payload.Model);
        Assert.True(payload.HasSerialNumber);
        Assert.False(payload.HasFirmwareVersion);
    }

    [Fact]
    public void MapStatusWithoutConnectorTypeEnrichment()
    {
        var payload = new StatusNotificationEventMapper().Map(
            new StatusNotificationSnapshot(
                "station-1",
                2,
                1,
                "Available",
                new DateTime(2026, 11, 14, 10, 30, 0, DateTimeKind.Utc)));

        Assert.Equal("station-1", payload.ChargerId);
        Assert.Equal(2, payload.EvseId);
        Assert.Equal(1, payload.ConnectorId);
        Assert.Equal("Available", payload.ConnectorStatus);
        Assert.Equal(new DateTime(2026, 11, 14, 10, 30, 0, DateTimeKind.Utc), payload.StatusTimestamp.ToDateTime());
#pragma warning disable CS0612 // Intentionally assert the deprecated field stays absent.
        Assert.False(payload.HasConnectorType);
#pragma warning restore CS0612
    }

    [Fact]
    public void MapTransactionLifecycleOptionalConsumption()
    {
        var mapper = new TransactionLifecycleEventMapper();

        var withoutConsumption = mapper.Map(
            new TransactionLifecycleSnapshot("station-1", "transaction-1", null));
        var withConsumption = mapper.Map(
            new TransactionLifecycleSnapshot("station-1", "transaction-1", 12.5m));

        Assert.False(withoutConsumption.HasConsumptionWh);
        Assert.True(withConsumption.HasConsumptionWh);
        Assert.Equal(12.5, withConsumption.ConsumptionWh);
    }

    [Fact]
    public void MapRegistrationInventoryPreservingUnknownConnectorTypes()
    {
        var payload = new ChargerRegistrationCompletedEventMapper().Map(
            new ChargerRegistrationSnapshot(
                "station-1",
                [
                    new RegisteredConnectorSnapshot(2, 1, null),
                    new RegisteredConnectorSnapshot(1, 2, "Type2"),
                ]));

        Assert.Equal("station-1", payload.ChargerId);
        Assert.Collection(
            payload.Connectors,
            first =>
            {
                Assert.Equal(1, first.EvseId);
                Assert.Equal(2, first.ConnectorId);
                Assert.True(first.HasConnectorType);
                Assert.Equal("Type2", first.ConnectorType);
            },
            second =>
            {
                Assert.Equal(2, second.EvseId);
                Assert.Equal(1, second.ConnectorId);
                Assert.False(second.HasConnectorType);
            });
    }
}
