namespace FrenchExDev.Net.Requirements.Attributes;

using System;
using FrenchExDev.Net.Dsl;
using FrenchExDev.Net.Requirements.Attributes.Concepts;

[MetaConcept(typeof(VerifiesConcept))]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class VerifiesAttribute : Attribute
{
    public Type RequirementType { get; }
    public string AcceptanceCriterionName { get; }

    public VerifiesAttribute(Type requirementType, string acceptanceCriterionName)
    {
        RequirementType = requirementType;
        AcceptanceCriterionName = acceptanceCriterionName;
    }
}
