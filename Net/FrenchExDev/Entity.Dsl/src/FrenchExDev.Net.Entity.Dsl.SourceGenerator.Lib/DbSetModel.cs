namespace FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

public sealed class DbSetModel
{
    public string EntityTypeFull { get; set; } = "";
    public string PropertyName { get; set; } = "";  // Pluralized class name
    public bool IsKeyless { get; set; }
}
