namespace FrenchExDev.Net.Requirements.Attributes.Concepts;

using System;
using FrenchExDev.Net.Dsl;

public sealed class ForRequirementConcept : MetaConcept
{
    public override string Name => "ForRequirement";
    public override Type AttributeType => typeof(ForRequirementAttribute);
}
