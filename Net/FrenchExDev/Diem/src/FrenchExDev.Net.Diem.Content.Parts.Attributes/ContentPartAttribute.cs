namespace FrenchExDev.Net.Diem.Content.Parts.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Content.Parts.Attributes.Concepts;

    [MetaConcept(typeof(ContentPartConcept))]
    [MetaConstraint("MustHaveField", nameof(MustHaveFieldConstraint),
        Message = "Content part must have at least one [PartField]")]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ContentPartAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("Description", "string")]
        public string Description { get; set; } = "";

        public ContentPartAttribute(string name) { Name = name; Description = ""; }

        public static ConstraintResult MustHaveFieldConstraint(ConceptValidationContext ctx)
        {
            foreach (var p in ctx.Properties)
            {
                foreach (var a in p.AttributeNames)
                {
                    if (a == "PartField") return ConstraintResult.Satisfied();
                }
            }
            return ConstraintResult.Failed("Content part must have at least one [PartField]");
        }
    }
}
