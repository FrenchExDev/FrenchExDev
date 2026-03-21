using System.Reflection;
using FrenchExDev.Net.Dsl;
using FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes;
using FrenchExDev.Net.Diem.Workflow.Gates.Attributes;
using FrenchExDev.Net.Diem.Workflow.Gates.Attributes.Concepts;
using FrenchExDev.Net.Diem.Workflow.Scheduling.Attributes;
using FrenchExDev.Net.Diem.Workflow.Locales.Attributes;
using Xunit;

namespace FrenchExDev.Net.Diem.Workflow.Tests;

public class WorkflowAttributeTests
{
    [Theory]
    [InlineData(typeof(WorkflowAttribute))]
    [InlineData(typeof(HasWorkflowAttribute))]
    [InlineData(typeof(StageAttribute))]
    [InlineData(typeof(TransitionAttribute))]
    [InlineData(typeof(GateAttribute))]
    [InlineData(typeof(RequiresRoleAttribute))]
    [InlineData(typeof(RequiresApprovalAttribute))]
    [InlineData(typeof(ScheduledTransitionAttribute))]
    [InlineData(typeof(ForEachLocaleAttribute))]
    public void AllWorkflowAttributes_HaveMetaConceptAttribute(Type attributeType)
    {
        var metaConcept = attributeType.GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(metaConcept);
        Assert.NotNull(metaConcept.ConceptType);
    }

    [Fact]
    public void RequiresRoleAttribute_InheritsFromGateConcept()
    {
        var metaInherits = typeof(RequiresRoleAttribute).GetCustomAttribute<MetaInheritsAttribute>();
        Assert.NotNull(metaInherits);
        Assert.Equal(typeof(GateConcept), metaInherits.ParentConceptType);
    }

    [Fact]
    public void RequiresApprovalAttribute_InheritsFromGateConcept()
    {
        var metaInherits = typeof(RequiresApprovalAttribute).GetCustomAttribute<MetaInheritsAttribute>();
        Assert.NotNull(metaInherits);
        Assert.Equal(typeof(GateConcept), metaInherits.ParentConceptType);
    }

    [Fact]
    public void RequiresRoleConcept_SuperTypes_ContainsGateConcept()
    {
        var concept = new RequiresRoleConcept();
        Assert.Contains(typeof(GateConcept), concept.SuperTypes);
    }

    [Fact]
    public void RequiresApprovalConcept_SuperTypes_ContainsGateConcept()
    {
        var concept = new RequiresApprovalConcept();
        Assert.Contains(typeof(GateConcept), concept.SuperTypes);
    }
}
