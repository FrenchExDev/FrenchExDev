namespace FrenchExDev.Net.Requirements.Attributes;

using System;
using FrenchExDev.Net.Dsl;
using FrenchExDev.Net.Requirements.Attributes.Concepts;

[MetaConcept(typeof(ForRequirementConcept))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ForRequirementAttribute : Attribute
{
    public Type RequirementType { get; }
    public string? AcceptanceCriterion { get; }

    public ForRequirementAttribute(Type requirementType, string? acceptanceCriterion = null)
    {
        RequirementType = requirementType;
        AcceptanceCriterion = acceptanceCriterion;
    }
}
