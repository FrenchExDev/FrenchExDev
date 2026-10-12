namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class AggregationConcept : MetaConcept
    {
        public override string Name => "Aggregation";
        public override Type AttributeType => typeof(AggregationAttribute);
    }
}
