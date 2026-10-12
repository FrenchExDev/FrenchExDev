namespace FrenchExDev.Net.Ddd.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Ddd.Attributes.Concepts;

    [MetaConcept(typeof(AggregateRootConcept))]
    [MetaInherits(typeof(EntityConcept))]
    [MetaConstraint("MustHaveId", nameof(MustHaveIdConstraint),
        Message = "Aggregate root must have an [EntityId] property")]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class AggregateRootAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("BoundedContext", "string")]
        public string? BoundedContext { get; set; }

        public AggregateRootAttribute(string name) { Name = name; }

        public static ConstraintResult MustHaveIdConstraint(ConceptValidationContext ctx)
        {
            foreach (var p in ctx.Properties)
            {
                foreach (var a in p.AttributeNames)
                {
                    if (a == "EntityId") return ConstraintResult.Satisfied();
                }
            }
            return ConstraintResult.Failed("Aggregate root must have an [EntityId] property");
        }
    }
}
