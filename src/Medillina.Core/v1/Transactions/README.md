# OCPP Charging Transactions
The underlying representation of an **OCPP transaction** is modeled as a *transaction graph*.  
Transaction graphs are **tree structures** that capture information about the ongoing transaction’s energy consumption. They store sampled values derived from meter readings in *Sink* nodes, organized by their **Context**, **Measurand**, and **Measurand Source**.  

The diagram below illustrates the structure of such a graph—**red paths** indicate preferred computation routes, which take priority *when present*.


<p align="center">
    <img src="/third_party/txgraph.jpg" />
</p>

Transaction Graphs can be merged, either inline by injecting raw meter values to an existing graph, or via the `<<` operator overload. This allows transactions to be kept up to date, regardless of the scenario. It also makes TxGraphs much easier to work with, because they treat consumption calculation as a basic mathematical model, of simply adding the relevant metrics together, linked by their characteristics.

If you choose to merge via operator overload, you can merge several graphs all at once:
```csharp
var graph1 = ....;
var graph2 = ....;
var graph3 = ....;
var result = graph1 << graph2 << graph3;  // merge (G3 -> G2) -> G1

var finalConsumption = result.Compute();
```

## Transaction Graphs vs Consumption Graphs
The distinction between a **TxGraph** and a **ConsumptionGraph** lies in their scope and purpose.  
A *TxGraph* represents the consumption data for a **specific transaction context**—such as `Transaction.Begin`, `Transaction.Sample`, or `Transaction.End`. Each TxGraph computes a discrete consumption value within that narrow frame.  

A *ConsumptionGraph*, on the other hand, aggregates **all relevant TxGraphs** belonging to a single transaction. It exists to encapsulate the *entire* transaction’s lifecycle and provide a holistic view of its energy consumption.  

You **cannot** obtain the final consumption value of a ConsumptionGraph by merely invoking `Compute()` as you would with a TxGraph. That’s because computing total consumption involves **business logic** that extends beyond the raw graph mechanics—it requires interpreting contextual rules, not just performing arithmetic over nodes.

## Transaction Graph - Benchmarks
The following benchmarks are ran 900k times, for 5 laps. Finally, the result is averaged.

| **Test Name** | **Description** | **Run 1 (ms)** | **Run 2 (ms)** | **Run 3 (ms)** | **Run 4 (ms)** | **Run 5 (ms)** | **Average (ms)** | **Relative Speed** |
|----------------|-----------------|---------------:|---------------:|---------------:|---------------:|---------------:|-----------------:|-------------------:|
| **First-Run** | Build full graph from raw data each loop | 3665 | 3651 | 3777 | 3639 | 3566 | **3659.6** | 1.0× |
| **Copy** | Duplicate existing graph only | 655 | 647 | 644 | 644 | 639 | **645.8** | - |
| **Merge-Inline** | Merge raw meter values inside current graph | 4094 | 4145 | 4130 | 4130 | 4116 | **4123.0** | ≈0.9× (slightly slower) |
| **Merge-Op** | Merge pre-built graphs (`graph1 << graph2`) | 1808 | 1791 | 1792 | 1784 | 1794 | **1793.8** | ~2.0× faster |

**NOTE**: Inline merging benchmark takes into account the time to also call `Copy()` on the graph.

Benchmark code:
```csharp
using System;
using System.Diagnostics;
using System.Linq;

void Benchmark(string label, Action action, int iterations = 5)
{
    var stopwatch = new Stopwatch();
    var results = new long[iterations];

    for (int run = 0; run < iterations; run++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        stopwatch.Restart();
        action();
        stopwatch.Stop();

        results[run] = stopwatch.ElapsedMilliseconds;
    }

    var average = results.Average();
    Console.WriteLine($"------ {label} ------");
    for (int i = 0; i < iterations; i++)
        Console.WriteLine($"   Run {i + 1}: {results[i]} ms");
    Console.WriteLine($"   Average: {average:F2} ms");
    Console.WriteLine();
}

/*
    ...omitted warm-up iterations
*/

// --- First Run ---
Benchmark("Elapsed time (First-Run)", () =>
{
    for (int i = 0; i < 900_000; i++)
    {
        var x = txService.GetConsumptionGraph(request1.MeterValue);
        txService.GetTransactionConsumption(x);
    }
});

// --- Copy ---
Benchmark("Elapsed time (Copy)", () =>
{
    for (int i = 0; i < 900_000; i++)
    {
        graph1.Copy();
    }
});

// --- Merge-Inline ---
Benchmark("Elapsed time (Merge-Inline)", () =>
{
    for (int i = 0; i < 900_000; i++)
    {
        txService.GetTransactionConsumption(request2.MeterValue, graph1.Copy());
    }
});

// --- Merge-Operator ---
Benchmark("Elapsed time (Merge-Op)", () =>
{
    for (int i = 0; i < 900_000; i++)
    {
        var result = graph1 << graph2;
        txService.GetTransactionConsumption(result);
    }
});

```
