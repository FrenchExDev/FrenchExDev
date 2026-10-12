namespace FrenchExDev.Net.Diem.Admin.Actions.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Diem.Admin.Actions.Attributes.Concepts;

    [MetaConcept(typeof(AdminActionConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class AdminActionAttribute : Attribute
    {
        [MetaProperty("Name", "string", Required = true)]
        public string Name { get; set; }

        [MetaProperty("Command", "string", Required = true)]
        public string Command { get; set; } = null!;

        [MetaProperty("Icon", "string")]
        public string? Icon { get; set; }

        [MetaProperty("ConfirmationMessage", "string")]
        public string? ConfirmationMessage { get; set; }

        [MetaProperty("RequiresRole", "string")]
        public string? RequiresRole { get; set; }

        public AdminActionAttribute(string name) { Name = name; }
    }
}
