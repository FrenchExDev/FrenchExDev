namespace FrenchExDev.Net.Requirements.Attributes.Concepts;

using System;
using FrenchExDev.Net.Dsl;

public sealed class VerifiesConcept : MetaConcept
{
    public override string Name => "Verifies";
    public override Type AttributeType => typeof(VerifiesAttribute);
}
