namespace FrenchExDev.Net.Dsl.Tests;

using FrenchExDev.Net.Dsl;
using FrenchExDev.Net.Dsl.Concepts;
using Xunit;

public class MetaConceptAttributeTests
{
    [Fact]
    public void MetaConcept_is_self_describing()
    {
        // M3 fixed point: MetaConceptAttribute is decorated with [MetaConcept]
        var attr = (MetaConceptAttribute?)Attribute.GetCustomAttribute(
            typeof(MetaConceptAttribute), typeof(MetaConceptAttribute));

        Assert.NotNull(attr);
        Assert.Equal(typeof(MetaConceptConcept), attr!.ConceptType);
    }

    [Fact]
    public void All_five_M3_primitives_are_self_describing()
    {
        AssertHasMetaConcept(typeof(MetaConceptAttribute));
        AssertHasMetaConcept(typeof(MetaPropertyAttribute));
        AssertHasMetaConcept(typeof(MetaReferenceAttribute));
        AssertHasMetaConcept(typeof(MetaConstraintAttribute));
        AssertHasMetaConcept(typeof(MetaInheritsAttribute));
    }

    private static void AssertHasMetaConcept(Type type)
    {
        var attr = Attribute.GetCustomAttribute(type, typeof(MetaConceptAttribute));
        Assert.NotNull(attr);
    }
}

public class MetaConceptCompanionTests
{
    [Fact]
    public void MetaConceptConcept_has_correct_name()
    {
        var concept = new MetaConceptConcept();
        Assert.Equal("MetaConcept", concept.Name);
        Assert.Equal(typeof(MetaConceptAttribute), concept.AttributeType);
    }

    [Fact]
    public void Validate_returns_satisfied_by_default()
    {
        var concept = new MetaConceptConcept();
        var ctx = new ConceptValidationContext { ConceptName = "Test", TypeName = "TestType" };
        var result = concept.Validate(ctx);
        Assert.True(result.IsSatisfied);
    }
}

public class ConstraintResultTests
{
    [Fact]
    public void Satisfied_is_satisfied()
    {
        var result = ConstraintResult.Satisfied();
        Assert.True(result.IsSatisfied);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Failed_has_message()
    {
        var result = ConstraintResult.Failed("bad");
        Assert.False(result.IsSatisfied);
        Assert.Equal("bad", result.Message);
    }

    [Fact]
    public void Aggregate_all_satisfied_returns_satisfied()
    {
        var result = ConstraintResult.Aggregate(new[]
        {
            ConstraintResult.Satisfied(),
            ConstraintResult.Satisfied()
        });
        Assert.True(result.IsSatisfied);
    }

    [Fact]
    public void Aggregate_with_failure_returns_failed()
    {
        var result = ConstraintResult.Aggregate(new[]
        {
            ConstraintResult.Satisfied(),
            ConstraintResult.Failed("err1"),
            ConstraintResult.Failed("err2")
        });
        Assert.False(result.IsSatisfied);
        Assert.Contains("err1", result.Message);
        Assert.Contains("err2", result.Message);
    }

    [Fact]
    public void Equality_works()
    {
        Assert.Equal(ConstraintResult.Satisfied(), ConstraintResult.Satisfied());
        Assert.NotEqual(ConstraintResult.Satisfied(), ConstraintResult.Failed("x"));
    }
}
