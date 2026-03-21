namespace FrenchExDev.Net.Diem.Workflow.Gates.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Workflow.Gates.Attributes.Concepts;

    [MetaConcept(typeof(RequiresApprovalConcept))]
    [MetaInherits(typeof(GateConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequiresApprovalAttribute : Attribute
    {
        [MetaProperty("Transition", "string", Required = true)]
        public string Transition { get; set; }

        [MetaProperty("MinApprovers", "int", Required = true)]
        public int MinApprovers { get; set; }

        public RequiresApprovalAttribute(string transition, int minApprovers)
        { Transition = transition; MinApprovers = minApprovers; }
    }
}
