using System.Text.Json;
using Medinilla.Core.Logic.Transactions;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure;

namespace Medinilla.Core.Tests;

public class TransactionEventActionShould
{
    // Based on the JSON data:
    // - Total Energy.Active.Import.Register at Transaction.End: 51 Wh = 0.051 kWh
    // - Per phase L1/L2/L3: 17 Wh each = 0.017 kWh each
    // NOTE: The current implementation returns 0.017 kWh (single phase value)
    // This constant represents what we EXPECT based on the total consumption in the JSON
    private const decimal EXPECTED_TOTAL_CONSUMPTION_KWH_1 = 0.051M; // Total: 51 Wh = 0.051 kWh
    private const decimal EXPECTED_TOTAL_CONSUMPTION_KWH_2 = 0.041M;

    private async Task AssertCalculation(string fileName, decimal expected)
    {
        // Arrange
        var jsonPath = Path.Combine("Data", fileName);
        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        var jsonArray = JsonDocument.Parse(jsonContent).RootElement;

        // Extract the payload (4th element in OCPP JSON array)
        var payloadJson = jsonArray[3].GetRawText();
        var request = JsonSerializer.Deserialize<TransactionEventRequest>(payloadJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new DottedEnumJsonConverter() }
        });

        Assert.NotNull(request);
        Assert.NotNull(request.MeterValue);

        // Create the TransactionService (this is what actually calculates consumption)
        var transactionService = new TransactionService();

        // Act - Calculate consumption from meter values
        // The JSON has meter values at Transaction.Begin and Transaction.End
        // The service processes these to determine the consumption
        var consumption = transactionService.GetTransactionConsumption(request.MeterValue);

        // Assert
        Assert.NotNull(consumption);

        // CURRENT BEHAVIOR: The implementation returns the LAST Energy.Active.Import.Register value found
        // In the JSON, the order is: total (51 Wh), L1 (17 Wh), L2 (17 Wh), L3 (17 Wh)
        // The service iterates and the last one (L3) becomes the result: 0.017 kWh
        Assert.Equal(expected, consumption.Consumption);

        // EXPECTED BEHAVIOR: Should use the total value without phase (51 Wh)
        // Assert.Equal(EXPECTED_TOTAL_CONSUMPTION_KWH, consumption.Consumption); // Uncomment when fixed

        Assert.Equal(Medinilla.DataTypes.Core.Enums.ConsumptionType.Cumulative, consumption.ConsumptionType);
    }

    [Fact]
    public async Task CalculateCorrectConsumptionFromTransactionEventEndJson()
    {
        await AssertCalculation("TransactionEventEnd.json", EXPECTED_TOTAL_CONSUMPTION_KWH_1);
        await AssertCalculation("TransactionEventEnd2.json", EXPECTED_TOTAL_CONSUMPTION_KWH_2);
    }
}
