using System.Collections.Concurrent;
using FrenchExDev.Net.BinaryWrapper.Design;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Tests;

public class HelpScraperConcurrencyTests
{
    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task NestedCommands_ShareOneLimit_AndKeepDiscoveryOrder(int limit)
    {
        var release = NewSignal();
        var calls = new ConcurrentDictionary<string, int>();
        var active = 0;
        var peak = 0;
        var scraper = new HelpScraper(new StandardHelpParser(), async args =>
        {
            calls.AddOrUpdate(string.Join(" ", args), 1, (_, count) => count + 1);
            if (args.Length == 2)
                return "Commands:\n  a    Branch A\n  b    Branch B\n  c    Branch C\n";
            if (args.Length == 3)
                return "Commands:\n  x    Leaf X\n  y    Leaf Y\n  z    Leaf Z\n";

            var current = Interlocked.Increment(ref active);
            int previous;
            do { previous = Volatile.Read(ref peak); }
            while (current > previous && Interlocked.CompareExchange(ref peak, current, previous) != previous);
            try
            {
                await release.Task;
                return "Leaf details\n";
            }
            finally { Interlocked.Decrement(ref active); }
        }, maxConcurrency: limit);

        // Completed parent help allows synchronous discovery up to the blocked leaves.
        // Each branch having its own semaphore would start more than 'limit' leaves.
        var scrape = scraper.ScrapeAsync("tool");
        var initiallyActive = Volatile.Read(ref active);
        release.TrySetResult();
        var tree = await scrape.WaitAsync(TimeSpan.FromSeconds(10));

        initiallyActive.ShouldBe(limit);
        peak.ShouldBe(limit);
        active.ShouldBe(0);
        calls.Count.ShouldBe(13);
        calls.Values.ShouldAllBe(count => count == 1);
        tree.Root.SubCommands.Select(node => node.Name).ShouldBe(new[] { "a", "b", "c" });
        foreach (var branch in tree.Root.SubCommands)
        {
            branch.SubCommands.Select(node => node.Name).ShouldBe(new[] { "x", "y", "z" });
            branch.SubCommands.ShouldAllBe(node => node.Description == "Leaf details");
        }
    }

    [Fact]
    public async Task FailedHelp_ReleasesPermit_AndPreservesStub()
    {
        var calls = new List<string>();
        var scraper = new HelpScraper(new StandardHelpParser(), args =>
        {
            calls.Add(string.Join(" ", args));
            if (args.Length == 2)
                return Task.FromResult("Commands:\n  bad    Original description\n  good    Original good\n");
            if (args[1] == "bad")
                throw new InvalidOperationException("Help unavailable");
            return Task.FromResult("Collected good\n");
        }, maxConcurrency: 1);

        var tree = await scraper.ScrapeAsync("tool").WaitAsync(TimeSpan.FromSeconds(10));

        calls.Count.ShouldBe(3);
        tree.Root.SubCommands[0].Description.ShouldBe("Original description");
        tree.Root.SubCommands[1].Description.ShouldBe("Collected good");
    }

    [Fact]
    public async Task CancellationWhileWaiting_DoesNotStartQueuedHelp_OrHang()
    {
        using var cancellation = new CancellationTokenSource();
        var release = NewSignal();
        var calls = new ConcurrentQueue<string>();
        var scraper = new HelpScraper(new StandardHelpParser(), async args =>
        {
            calls.Enqueue(string.Join(" ", args));
            if (args.Length == 2)
                return "Commands:\n  a    First\n  b    Second\n";
            await release.Task;
            return "Collected first\n";
        }, maxConcurrency: 1);

        var scrape = scraper.ScrapeAsync("tool", cancellation.Token);
        cancellation.Cancel();
        release.TrySetResult();
        var tree = await scrape.WaitAsync(TimeSpan.FromSeconds(10));

        calls.ToArray().ShouldBe(new[] { "tool --help", "tool a --help" });
        // Preserve the scraper's existing best-effort cancellation contract.
        tree.BinaryName.ShouldBe("tool");
    }

    [Fact]
    public async Task ConcurrentScrapes_HaveIndependentLimits()
    {
        var release = NewSignal();
        var active = 0;
        var scraper = new HelpScraper(new StandardHelpParser(), async _ =>
        {
            Interlocked.Increment(ref active);
            try
            {
                await release.Task;
                return "Root details\n";
            }
            finally { Interlocked.Decrement(ref active); }
        }, maxConcurrency: 1);

        var first = scraper.ScrapeAsync("first");
        var second = scraper.ScrapeAsync("second");
        var initiallyActive = Volatile.Read(ref active);
        release.TrySetResult();
        var trees = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

        initiallyActive.ShouldBe(2);
        active.ShouldBe(0);
        trees.Select(tree => tree.BinaryName).ShouldBe(new[] { "first", "second" });
    }
}

