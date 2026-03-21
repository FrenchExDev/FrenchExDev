namespace FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes.Concepts;

    [MetaConcept(typeof(TransitionConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TransitionAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("From", "string", Required = true)]
        public string From { get; set; } = "";

        [MetaProperty("To", "string", Required = true)]
        public string To { get; set; } = "";

        [MetaProperty("Description", "string")]
        public string? Description { get; set; }

        public TransitionAttribute(string name) { Name = name; }
    }
}
