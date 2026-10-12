using FrenchExDev.Net.Diem.Workflow.Gates;
using Xunit;

namespace FrenchExDev.Net.Diem.Workflow.Tests;

public class GateResultTests
{
    [Fact]
    public void Allowed_ReturnsIsAllowedTrue()
    {
        var result = GateResult.Allowed();
        Assert.True(result.IsAllowed);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Denied_ReturnsIsAllowedFalse_WithReason()
    {
        var result = GateResult.Denied("Insufficient permissions");
        Assert.False(result.IsAllowed);
        Assert.Equal("Insufficient permissions", result.Reason);
    }
}
