using FrenchExDev.Net.QualityGate.Model;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class NamespaceMetricsTests
{
    [Fact]
    public void Abstractness_NoTypes_Returns0()
    {
        var ns = new NamespaceMetrics
        {
            Name = "Test", TypeCount = 0, AbstractTypeCount = 0,
            AfferentCoupling = 0, EfferentCoupling = 0
        };
        ns.Abstractness.ShouldBe(0);
    }

    [Fact]
    public void Abstractness_AllAbstract_Returns1()
    {
        var ns = new NamespaceMetrics
        {
            Name = "Test", TypeCount = 3, AbstractTypeCount = 3,
            AfferentCoupling = 0, EfferentCoupling = 0
        };
        ns.Abstractness.ShouldBe(1);
    }

    [Fact]
    public void Abstractness_HalfAbstract_ReturnsHalf()
    {
        var ns = new NamespaceMetrics
        {
            Name = "Test", TypeCount = 4, AbstractTypeCount = 2,
            AfferentCoupling = 0, EfferentCoupling = 0
        };
        ns.Abstractness.ShouldBe(0.5);
    }

    [Fact]
    public void Instability_NoCoupling_Returns0()
    {
        var ns = new NamespaceMetrics
        {
            Name = "Test", TypeCount = 1, AbstractTypeCount = 0,
            AfferentCoupling = 0, EfferentCoupling = 0
        };
        ns.Instability.ShouldBe(0);
    }

    [Fact]
    public void Instability_OnlyEfferent_Returns1()
    {
        var ns = new NamespaceMetrics
        {
            Name = "Test", TypeCount = 1, AbstractTypeCount = 0,
            AfferentCoupling = 0, EfferentCoupling = 5
        };
        ns.Instability.ShouldBe(1);
    }

    [Fact]
    public void Instability_OnlyAfferent_Returns0()
    {
        var ns = new NamespaceMetrics
        {
            Name = "Test", TypeCount = 1, AbstractTypeCount = 0,
            AfferentCoupling = 5, EfferentCoupling = 0
        };
        ns.Instability.ShouldBe(0);
    }

    [Fact]
    public void Instability_Equal_ReturnsHalf()
    {
        var ns = new NamespaceMetrics
        {
            Name = "Test", TypeCount = 1, AbstractTypeCount = 0,
            AfferentCoupling = 5, EfferentCoupling = 5
        };
        ns.Instability.ShouldBe(0.5);
    }

    [Fact]
    public void DistanceFromMainSequence_OnMainSequence_Returns0()
    {
        // A=0.5, I=0.5 → D=|0.5+0.5-1|=0
        var ns = new NamespaceMetrics
        {
            Name = "Test", TypeCount = 2, AbstractTypeCount = 1,
            AfferentCoupling = 5, EfferentCoupling = 5
        };
        ns.DistanceFromMainSequence.ShouldBe(0);
    }

    [Fact]
    public void DistanceFromMainSequence_ConcreteStable_Returns1()
    {
        // A=0, I=0 → D=|0+0-1|=1
        var ns = new NamespaceMetrics
        {
            Name = "Test", TypeCount = 2, AbstractTypeCount = 0,
            AfferentCoupling = 5, EfferentCoupling = 0
        };
        ns.DistanceFromMainSequence.ShouldBe(1);
    }

    [Fact]
    public void DistanceFromMainSequence_AbstractUnstable_Returns1()
    {
        // A=1, I=1 → D=|1+1-1|=1
        var ns = new NamespaceMetrics
        {
            Name = "Test", TypeCount = 2, AbstractTypeCount = 2,
            AfferentCoupling = 0, EfferentCoupling = 5
        };
        ns.DistanceFromMainSequence.ShouldBe(1);
    }
}
