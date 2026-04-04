namespace FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

using System.Collections.Generic;

public sealed class EntityEmitModel
{
    public string Namespace { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string ClassFullName { get; set; } = "";

    // Table
    public string? TableName { get; set; }
    public string? Schema { get; set; }

    // Key
    public List<KeyPropertyModel> PrimaryKeyProperties { get; set; } = new List<KeyPropertyModel>();

    // Properties
    public List<PropertyConfigModel> Properties { get; set; } = new List<PropertyConfigModel>();
}
