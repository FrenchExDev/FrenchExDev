namespace FrenchExDev.Net.Ddd.Attributes.Concepts
{
    using System;
    using FrenchExDev.Net.Dsl;

    public sealed class CommandConcept : MetaConcept
    {
        public override string Name => "Command";
        public override Type AttributeType => typeof(CommandAttribute);
    }
}
