namespace FrenchExDev.Net.Requirements.Tests;

using FrenchExDev.Net.Requirements;
using FrenchExDev.Net.Requirements.Testing;
using Xunit;

public class RequirementHierarchyTests
{
    [Fact]
    public void Epic_is_RequirementMetadata()
    {
        Assert.True(typeof(RequirementMetadata).IsAssignableFrom(typeof(Epic)));
    }

    [Fact]
    public void Feature_with_epic_parent_compiles()
    {
        // SampleFeature : Feature<SampleEpic> -- generic constraint enforced by compiler
        Assert.True(typeof(RequirementMetadata).IsAssignableFrom(typeof(SampleFeature)));
    }

    [Fact]
    public void Story_with_feature_parent_compiles()
    {
        Assert.True(typeof(RequirementMetadata).IsAssignableFrom(typeof(SampleStory)));
    }

    [Fact]
    public void SampleFeature_has_two_acceptance_criteria()
    {
        var methods = typeof(SampleFeature).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        var acs = System.Array.FindAll(methods, m => m.IsAbstract && m.ReturnType == typeof(AcceptanceCriterionResult));
        Assert.Equal(2, acs.Length);
    }
}

public class AcceptanceCriterionResultTests
{
    [Fact]
    public void Satisfied_is_true()
    {
        var result = AcceptanceCriterionResult.Satisfied();
        Assert.True(result.IsSatisfied);
        Assert.True(result); // implicit bool conversion
    }

    [Fact]
    public void Failed_is_false_with_reason()
    {
        var result = AcceptanceCriterionResult.Failed("bad input");
        Assert.False(result.IsSatisfied);
        Assert.False(result);
        Assert.Equal("bad input", result.FailureReason);
    }

    [Fact]
    public void Equality_works()
    {
        Assert.Equal(AcceptanceCriterionResult.Satisfied(), AcceptanceCriterionResult.Satisfied());
        Assert.NotEqual(AcceptanceCriterionResult.Satisfied(), AcceptanceCriterionResult.Failed("x"));
    }
}

public class AttributeTests
{
    [Fact]
    public void ForRequirement_has_MetaConcept()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(FrenchExDev.Net.Requirements.Attributes.ForRequirementAttribute),
            typeof(FrenchExDev.Net.Dsl.MetaConceptAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    public void Verifies_has_MetaConcept()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(FrenchExDev.Net.Requirements.Attributes.VerifiesAttribute),
            typeof(FrenchExDev.Net.Dsl.MetaConceptAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    public void TestsFor_has_MetaConcept()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(FrenchExDev.Net.Requirements.Attributes.TestsForAttribute),
            typeof(FrenchExDev.Net.Dsl.MetaConceptAttribute));
        Assert.NotNull(attr);
    }
}
