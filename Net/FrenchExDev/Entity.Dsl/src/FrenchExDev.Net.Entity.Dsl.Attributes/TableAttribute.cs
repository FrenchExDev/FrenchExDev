namespace FrenchExDev.Net.Entity.Dsl.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Entity.Dsl.Attributes.Concepts;

    [MetaConcept(typeof(TableConcept))]
    [MetaConstraint("RequiresEntity", nameof(RequiresEntityConstraint),
        Message = "Table must be applied to a class with [Entity] or [AggregateRoot]")]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class TableAttribute : Attribute
    {
        [MetaProperty("Name", "string")]
        public string? Name { get; set; }

        [MetaProperty("Schema", "string")]
        public string? Schema { get; set; }

        public static ConstraintResult RequiresEntityConstraint(ConceptValidationContext ctx)
        {
            foreach (var a in ctx.ClassAttributes)
            {
                if (a.Name == "Entity" || a.Name == "AggregateRoot")
                    return ConstraintResult.Satisfied();
            }
            return ConstraintResult.Failed("Table must be applied to a class with [Entity] or [AggregateRoot]");
        }
    }
}
