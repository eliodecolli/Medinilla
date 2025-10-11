using System.Text.Json;
using Medinilla.Core.v1.Transactions;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure;

namespace Medinilla.Core.Tests;

public class TransactionEventActionShould
{
    private const decimal EXPECTED_TOTAL_CONSUMPTION_KWH_1 = 0.051M; // Total: 51 Wh = 0.051 kWh
    private const decimal EXPECTED_TOTAL_CONSUMPTION_KWH_2 = 0.041M; // Ditto

    private async Task<TransactionEventRequest> GetRequest(string fileName)
    {
        var jsonPath = Path.Combine("Data", fileName);
        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        var jsonArray = JsonDocument.Parse(jsonContent).RootElement;

        var payloadJson = jsonArray[3].GetRawText();
        var request = JsonSerializer.Deserialize<TransactionEventRequest>(payloadJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new DottedEnumJsonConverter() }
        });
        
        return request;
    }

    private async Task AssertCalculation(string fileName, decimal expected)
    {
        var request = await GetRequest(fileName);
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

    [Fact]
    public async Task MergesConsumptionGraphs()
    {
        var txService = new TransactionService();
        
        var request1 = await GetRequest("TransactionEventEnd.json");

        var graph1 = txService.GetConsumptionGraph(request1.MeterValue);
        Assert.NotNull(graph1);

        var request2 = await GetRequest("TransactionEventEnd2.json");
        
        var graph2 =  txService.GetConsumptionGraph(request2.MeterValue);
        Assert.NotNull(graph2);
        
        Assert.Equal(EXPECTED_TOTAL_CONSUMPTION_KWH_1, txService.GetTransactionConsumption(graph1).Consumption);
        Assert.Equal(EXPECTED_TOTAL_CONSUMPTION_KWH_2, txService.GetTransactionConsumption(graph2).Consumption);
        
        var expected = EXPECTED_TOTAL_CONSUMPTION_KWH_1 + EXPECTED_TOTAL_CONSUMPTION_KWH_2;
        Assert.Equal(expected, txService.GetTransactionConsumption(request2.MeterValue, graph1).Consumption);
    }

    [Fact]
    public async Task MergesConsumptionGraphsByOperator()
    {
        var txService =  new TransactionService();
        
        var request1 = await GetRequest("TransactionEventEnd.json");

        var graph1 = txService.GetConsumptionGraph(request1.MeterValue);
        Assert.NotNull(graph1);

        var request2 = await GetRequest("TransactionEventEnd2.json");
        
        var graph2 =  txService.GetConsumptionGraph(request2.MeterValue);
        Assert.NotNull(graph2);
        
        Assert.Equal(EXPECTED_TOTAL_CONSUMPTION_KWH_1, txService.GetTransactionConsumption(graph1).Consumption);
        Assert.Equal(EXPECTED_TOTAL_CONSUMPTION_KWH_2, txService.GetTransactionConsumption(graph2).Consumption);
        
        var expected = EXPECTED_TOTAL_CONSUMPTION_KWH_1 + EXPECTED_TOTAL_CONSUMPTION_KWH_2;
        
        var finalGraph = graph1 << graph2;
        Assert.NotNull(finalGraph);
        Assert.Equal(expected, txService.GetTransactionConsumption(finalGraph).Consumption);

        var ff = finalGraph << graph2 << graph1;
        Assert.NotNull(ff);
        Assert.Equal(expected * 2, txService.GetTransactionConsumption(ff).Consumption);
    }
}
