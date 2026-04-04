namespace FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

public sealed class PropertyConfigModel
{
    public string PropertyName { get; set; } = "";
    public string? ColumnName { get; set; }
    public string? ColumnType { get; set; }
    public int ColumnOrder { get; set; } = -1;
    public bool IsRequired { get; set; }
    public int MaxLength { get; set; }
    public bool IsNotMapped { get; set; }
}
