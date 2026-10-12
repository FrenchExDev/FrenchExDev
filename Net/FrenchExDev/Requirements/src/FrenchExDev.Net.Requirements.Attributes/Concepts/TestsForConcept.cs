namespace FrenchExDev.Net.Requirements.Attributes.Concepts;

using System;
using FrenchExDev.Net.Dsl;

public sealed class TestsForConcept : MetaConcept
{
    public override string Name => "TestsFor";
    public override Type AttributeType => typeof(TestsForAttribute);
}
