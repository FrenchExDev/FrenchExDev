using FrenchExDev.Net.QualityGate.Analysis;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class SolutionLoaderTests
{
    [Fact]
    public async Task LoadAsync_NullPath_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => SolutionLoader.LoadAsync(null!));
    }

    [Fact]
    public async Task LoadAsync_EmptyPath_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => SolutionLoader.LoadAsync(""));
    }

    [Fact]
    public async Task LoadAsync_WhitespacePath_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => SolutionLoader.LoadAsync("   "));
    }
}
