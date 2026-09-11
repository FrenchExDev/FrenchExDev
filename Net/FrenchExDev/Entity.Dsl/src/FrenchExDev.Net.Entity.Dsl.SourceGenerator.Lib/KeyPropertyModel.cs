namespace FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

public sealed class KeyPropertyModel
{
    public string PropertyName { get; set; } = "";
    public int Order { get; set; }
    public string ValueGenerated { get; set; } = "OnAdd"; // None, OnAdd, OnUpdate, OnAddOrUpdate
}
