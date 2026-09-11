namespace FrenchExDev.Net.Diem.Workflow.Gates.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Workflow.Gates.Attributes.Concepts;

    [MetaConcept(typeof(RequiresRoleConcept))]
    [MetaInherits(typeof(GateConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequiresRoleAttribute : Attribute
    {
        [MetaProperty("Transition", "string", Required = true)]
        public string Transition { get; set; }

        [MetaProperty("Role", "string", Required = true)]
        public string Role { get; set; }

        public RequiresRoleAttribute(string transition, string role)
        { Transition = transition; Role = role; }
    }
}
