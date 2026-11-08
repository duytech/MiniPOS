using System.Reflection;
using Pos.Domain.Common;

namespace Pos.ArchitectureTests;

public class ResultUsageTests
{
    [Fact]
    public void No_layer_throws_away_a_result()
    {
        var offenders = Layers.All.SelectMany(CallScanner.ScanDiscardedResults).ToList();

        Assert.True(offenders.Count == 0,
            "A failed Result that nobody reads would be saved and answered as a success. Offending calls: " +
            string.Join(", ", offenders));
    }

    [Theory]
    [InlineData(nameof(Samples.DiscardsResult))]
    [InlineData(nameof(Samples.DiscardsResultWithData))]
    [InlineData(nameof(Samples.DiscardsAwaitedResult))]
    public void The_scanner_finds_a_discarded_result(string sample)
    {
        var offenders = CallScanner.ScanDiscardedResults(Assembly.GetExecutingAssembly());

        Assert.Contains(offenders, o => o.Caller.EndsWith($"Samples.{sample}", StringComparison.Ordinal));
    }

    [Fact]
    public void The_scanner_accepts_a_result_that_is_read()
    {
        var offenders = CallScanner.ScanDiscardedResults(Assembly.GetExecutingAssembly());

        Assert.DoesNotContain(offenders, o => o.Caller.EndsWith($"Samples.{nameof(Samples.ReadsResult)}", StringComparison.Ordinal));
    }

    private static class Samples
    {
        public static void DiscardsResult() => Result.Ok();

        public static void DiscardsResultWithData() => Result.Ok(1);

        public static async Task DiscardsAwaitedResult() => await Task.FromResult(Result.Ok());

        public static bool ReadsResult() => Result.Ok().IsSuccess;
    }
}
