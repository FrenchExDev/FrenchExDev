namespace FrenchExDev.Net.Requirements.Attributes;

using System;
using FrenchExDev.Net.Dsl;
using FrenchExDev.Net.Requirements.Attributes.Concepts;

[MetaConcept(typeof(TestsForConcept))]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TestsForAttribute : Attribute
{
    public Type RequirementType { get; }

    public TestsForAttribute(Type requirementType)
    {
        RequirementType = requirementType;
    }
}
