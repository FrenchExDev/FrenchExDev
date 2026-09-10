using FrenchExDev.Net.BinaryWrapper.Design;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Tests;

public sealed class ContainerRuntimeFailureTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task RuntimeFailure_AtAnyDepth_DoesNotWriteAnIncompleteSnapshot(int depth, bool existingSnapshot)
    {
        var directory = Path.Combine(Path.GetTempPath(), "bw-runtime-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "tool-1.0.json");
        try
        {
            if (existingSnapshot)
                await File.WriteAllTextAsync(output, "previous complete snapshot");
            var failure = new ContainerRuntimeException("Cannot connect to Podman");
            var pipeline = new ScrapePipeline()
                .Binary("tool")
                .WithRunHelp(args => args.Length == depth + 2
                    ? Task.FromException<string>(failure)
                    : Task.FromResult("Commands:\n  child    Child command\n"))
                .OutputTo(output);

            var error = await Should.ThrowAsync<ContainerRuntimeException>(() =>
                pipeline.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(10)));

            error.ShouldBeSameAs(failure);
            if (existingSnapshot)
                (await File.ReadAllTextAsync(output)).ShouldBe("previous complete snapshot");
            else
                File.Exists(output).ShouldBeFalse();
        }
        finally
        {
            File.Delete(output);
            Directory.Delete(directory);
        }
    }
}