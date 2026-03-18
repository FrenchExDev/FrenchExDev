using FrenchExDev.Net.QualityGate.Gates;

using Shouldly;

namespace FrenchExDev.Net.QualityGate.Tests;

public class QualityGateDefinitionTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        Func<double, double, bool> comparator = (actual, threshold) => actual >= threshold;
        var def = new QualityGateDefinition("Coverage", "Line coverage gate", 80.0, comparator);

        def.Name.ShouldBe("Coverage");
        def.Description.ShouldBe("Line coverage gate");
        def.Threshold.ShouldBe(80.0);
        def.Comparator.ShouldBe(comparator);
    }

    [Fact]
    public void Comparator_GreaterThanOrEqual_WorksCorrectly()
    {
        var def = new QualityGateDefinition("Gate", "desc", 50.0, (a, t) => a >= t);

        def.Comparator(60.0, 50.0).ShouldBeTrue();
        def.Comparator(50.0, 50.0).ShouldBeTrue();
        def.Comparator(40.0, 50.0).ShouldBeFalse();
    }

    [Fact]
    public void Comparator_LessThanOrEqual_WorksCorrectly()
    {
        var def = new QualityGateDefinition("Complexity", "desc", 10.0, (a, t) => a <= t);

        def.Comparator(5.0, 10.0).ShouldBeTrue();
        def.Comparator(10.0, 10.0).ShouldBeTrue();
        def.Comparator(15.0, 10.0).ShouldBeFalse();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        Func<double, double, bool> comp = (a, t) => a >= t;
        var a = new QualityGateDefinition("G", "D", 1.0, comp);
        var b = new QualityGateDefinition("G", "D", 1.0, comp);

        a.ShouldBe(b);
    }
}
