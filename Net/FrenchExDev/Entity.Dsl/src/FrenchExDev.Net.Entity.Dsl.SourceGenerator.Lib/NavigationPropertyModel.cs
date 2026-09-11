namespace FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

public sealed class NavigationPropertyModel
{
    public string PropertyName { get; set; } = "";
    public string OnDelete { get; set; } = "NoAction"; // Cascade, Restrict, NoAction, SetNull, ClientCascade
}
