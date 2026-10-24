using System.Text.Json;

namespace DotnetBenchmarkBot.Tests;

public class JsonBenchmarkReportParserDirectoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dbb-" + Guid.NewGuid().ToString("N"));

    public JsonBenchmarkReportParserDirectoryTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task ParseDirectoryAsync_TwoReportFiles_CombinesResultsInFileNameOrder()
    {
        // Written in reverse name order so the result proves sorting, not creation order.
        WriteReport("b-report-full.json", ("MyBenchmarks.Second()", 200.0));
        WriteReport("a-report-full.json", ("MyBenchmarks.First()", 100.0), ("MyBenchmarks.Third()", 300.0));

        var results = await new JsonBenchmarkReportParser().ParseDirectoryAsync(_directory);

        Assert.Equal(
            new[] { "MyBenchmarks.First()", "MyBenchmarks.Third()", "MyBenchmarks.Second()" },
            results.Select(r => r.FullName));
        Assert.Equal(100.0, results[0].MeanNanoseconds);
        Assert.Equal(200.0, results[2].MeanNanoseconds);
    }

    [Fact]
    public async Task ParseDirectoryAsync_NonMatchingFiles_AreIgnored()
    {
        WriteReport("Foo-report-full.json", ("MyBenchmarks.Matching()", 100.0));
        WriteReport("Foo-report.json", ("MyBenchmarks.WrongSuffix()", 200.0));
        WriteReport("Foo-report-full.csv", ("MyBenchmarks.WrongExtension()", 300.0));
        File.WriteAllText(Path.Combine(_directory, "notes.txt"), "not a report");

        var results = await new JsonBenchmarkReportParser().ParseDirectoryAsync(_directory);

        var result = Assert.Single(results);
        Assert.Equal("MyBenchmarks.Matching()", result.FullName);
    }

    [Fact]
    public async Task ParseDirectoryAsync_DuplicateFullName_KeepsFirstOccurrence()
    {
        WriteReport("a-report-full.json", ("MyBenchmarks.Shared()", 100.0));
        WriteReport("b-report-full.json", ("MyBenchmarks.Shared()", 999.0), ("MyBenchmarks.Unique()", 200.0));

        var results = await new JsonBenchmarkReportParser().ParseDirectoryAsync(_directory);

        Assert.Equal(2, results.Count);
        Assert.Equal("MyBenchmarks.Shared()", results[0].FullName);
        Assert.Equal(100.0, results[0].MeanNanoseconds);
        Assert.Equal("MyBenchmarks.Unique()", results[1].FullName);
    }

    [Fact]
    public async Task ParseDirectoryAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var results = await new JsonBenchmarkReportParser().ParseDirectoryAsync(_directory);

        Assert.Empty(results);
    }

    [Fact]
    public async Task ParseDirectoryAsync_NonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        var missing = Path.Combine(_directory, "does-not-exist");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => new JsonBenchmarkReportParser().ParseDirectoryAsync(missing));
    }

    [Fact]
    public async Task ParseDirectoryAsync_NestedSubdirectory_IsNotScanned()
    {
        WriteReport("top-report-full.json", ("MyBenchmarks.Top()", 100.0));

        var nested = Path.Combine(_directory, "nested");
        Directory.CreateDirectory(nested);
        WriteReport(Path.Combine("nested", "deep-report-full.json"), ("MyBenchmarks.Deep()", 200.0));

        var results = await new JsonBenchmarkReportParser().ParseDirectoryAsync(_directory);

        var result = Assert.Single(results);
        Assert.Equal("MyBenchmarks.Top()", result.FullName);
    }

    /// <summary>
    /// Writes a minimal BenchmarkDotNet full report with one statistics block per row.
    /// </summary>
    /// <param name="fileName">File name, or a relative path under the test directory.</param>
    /// <param name="rows">Benchmark full name and mean value in nanoseconds.</param>
    private void WriteReport(string fileName, params (string name, double mean)[] rows)
    {
        var report = new
        {
            Benchmarks = rows.Select(row => new
            {
                FullName = row.name,
                Statistics = new { Mean = row.mean, StandardDeviation = 1.0 },
            }).ToArray(),
        };

        File.WriteAllText(Path.Combine(_directory, fileName), JsonSerializer.Serialize(report));
    }
}
