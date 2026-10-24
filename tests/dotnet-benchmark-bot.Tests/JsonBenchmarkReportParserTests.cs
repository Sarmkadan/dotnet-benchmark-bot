using System.Text;
using System.Text.Json;

namespace DotnetBenchmarkBot.Tests;

public class JsonBenchmarkReportParserTests
{
    private const string TwoBenchmarksReport = """
        {
          "Title": "MyBenchmarks-20261024-120000",
          "HostEnvironmentInfo": {
            "BenchmarkDotNetVersion": "0.14.0",
            "OsVersion": "Ubuntu 24.04.1 LTS (Noble Numbat)",
            "ProcessorName": "AMD EPYC 7763 64-Core Processor",
            "PhysicalProcessorCount": 1,
            "PhysicalCoreCount": 4,
            "LogicalCoreCount": 8,
            "RuntimeVersion": ".NET 8.0.10",
            "Architecture": "X64",
            "HasAttachedDebugger": false,
            "HasRyuJit": true,
            "Configuration": "Release",
            "JitModules": [ "libclrjit.so" ],
            "DotNetCliVersion": "8.0.300"
          },
          "Benchmarks": [
            {
              "DisplayInfo": "MyBenchmarks.Parse: DefaultJob",
              "Namespace": "MyBenchmarks",
              "Type": "ParserBenchmarks",
              "Method": "Parse",
              "Parameters": "size: 1024",
              "FullName": "MyBenchmarks.Parse(size: 1024)",
              "Statistics": {
                "N": 20,
                "Min": 1210.4,
                "LowerFence": 1200.1,
                "Q1": 1224.0,
                "Median": 1230.0,
                "Mean": 1234.5,
                "Q3": 1245.2,
                "UpperFence": 1270.0,
                "Max": 1280.3,
                "StandardError": 2.75,
                "StandardDeviation": 12.3,
                "Variance": 151.29
              },
              "Memory": {
                "Gen0Collections": 1,
                "Gen1Collections": 0,
                "Gen2Collections": 0,
                "BytesAllocatedPerOperation": 2048
              }
            },
            {
              "DisplayInfo": "MyBenchmarks.Parse: DefaultJob",
              "Namespace": "MyBenchmarks",
              "Type": "ParserBenchmarks",
              "Method": "Parse",
              "Parameters": "size: 4096",
              "FullName": "MyBenchmarks.Parse(size: 4096)",
              "Statistics": {
                "N": 20,
                "Min": 4510.0,
                "LowerFence": 4480.5,
                "Q1": 4540.0,
                "Median": 4560.0,
                "Mean": 4567.8,
                "Q3": 4590.4,
                "UpperFence": 4650.0,
                "Max": 4702.1,
                "StandardError": 10.2,
                "StandardDeviation": 45.6,
                "Variance": 2079.36
              },
              "Memory": {
                "Gen0Collections": 2,
                "Gen1Collections": 0,
                "Gen2Collections": 0,
                "BytesAllocatedPerOperation": 8192
              }
            }
          ]
        }
        """;

    [Fact]
    public async Task ParseAsync_TwoBenchmarks_ReturnsBothWithMeasurements()
    {
        var results = await ParseAsync(TwoBenchmarksReport);

        Assert.Equal(2, results.Count);

        Assert.Equal("MyBenchmarks.Parse(size: 1024)", results[0].FullName);
        Assert.Equal(1234.5, results[0].MeanNanoseconds);
        Assert.Equal(12.3, results[0].StdDevNanoseconds);
        Assert.Equal(2048L, results[0].AllocatedBytes);

        Assert.Equal("MyBenchmarks.Parse(size: 4096)", results[1].FullName);
        Assert.Equal(4567.8, results[1].MeanNanoseconds);
        Assert.Equal(45.6, results[1].StdDevNanoseconds);
        Assert.Equal(8192L, results[1].AllocatedBytes);
    }

    [Fact]
    public async Task ParseAsync_BenchmarkWithoutMemory_HasNullAllocatedBytes()
    {
        const string json = """
            {
              "Title": "MyBenchmarks-20261024-120000",
              "HostEnvironmentInfo": { "RuntimeVersion": ".NET 8.0.10" },
              "Benchmarks": [
                {
                  "FullName": "MyBenchmarks.Parse(size: 1024)",
                  "Statistics": { "Mean": 1234.5, "StandardDeviation": 12.3, "Median": 1230.0 }
                }
              ]
            }
            """;

        var results = await ParseAsync(json);

        var result = Assert.Single(results);
        Assert.Equal("MyBenchmarks.Parse(size: 1024)", result.FullName);
        Assert.Equal(1234.5, result.MeanNanoseconds);
        Assert.Equal(12.3, result.StdDevNanoseconds);
        Assert.Null(result.AllocatedBytes);
    }

    [Fact]
    public async Task ParseAsync_BenchmarkWithNullStatistics_IsSkipped()
    {
        const string json = """
            {
              "Title": "MyBenchmarks-20261024-120000",
              "HostEnvironmentInfo": { "RuntimeVersion": ".NET 8.0.10" },
              "Benchmarks": [
                {
                  "FullName": "MyBenchmarks.Parse(size: 1024)",
                  "Statistics": null,
                  "Memory": { "BytesAllocatedPerOperation": 2048 }
                },
                {
                  "FullName": "MyBenchmarks.Parse(size: 4096)",
                  "Statistics": { "Mean": 4567.8, "StandardDeviation": 45.6, "Median": 4560.0 },
                  "Memory": { "BytesAllocatedPerOperation": 8192 }
                }
              ]
            }
            """;

        var results = await ParseAsync(json);

        var result = Assert.Single(results);
        Assert.Equal("MyBenchmarks.Parse(size: 4096)", result.FullName);
    }

    [Fact]
    public async Task ParseAsync_RootWithoutBenchmarks_ThrowsInvalidDataException()
    {
        const string json = """
            {
              "Title": "MyBenchmarks-20261024-120000",
              "HostEnvironmentInfo": { "RuntimeVersion": ".NET 8.0.10" }
            }
            """;

        await Assert.ThrowsAsync<InvalidDataException>(() => ParseAsync(json));
    }

    [Fact]
    public async Task ParseAsync_EmptyBenchmarksArray_ReturnsEmptyList()
    {
        const string json = """
            {
              "Title": "MyBenchmarks-20261024-120000",
              "HostEnvironmentInfo": { "RuntimeVersion": ".NET 8.0.10" },
              "Benchmarks": []
            }
            """;

        var results = await ParseAsync(json);

        Assert.Empty(results);
    }

    [Fact]
    public async Task ParseAsync_MalformedJson_ThrowsJsonException()
    {
        const string json = """
            {
              "Title": "MyBenchmarks-20261024-120000",
              "Benchmarks": [
                { "FullName": "MyBenchmarks.Parse(size: 1024)",
            """;

        // Truncated input surfaces as JsonReaderException, a subclass of JsonException.
        await Assert.ThrowsAnyAsync<JsonException>(() => ParseAsync(json));
    }

    [Fact]
    public async Task ParseAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ParseAsync(TwoBenchmarksReport, cancellationSource.Token));
    }

    private static Task<IReadOnlyList<BenchmarkResult>> ParseAsync(string json, CancellationToken cancellationToken = default)
    {
        var parser = new JsonBenchmarkReportParser();

        return parser.ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)), cancellationToken);
    }
}
