namespace FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

using System.Collections.Generic;

public sealed class RepositoryEmitModel
{
    public string Namespace { get; set; } = "";
    public string EntityClassName { get; set; } = "";
    public string EntityClassFull { get; set; } = "";
    public string PrimaryKeyTypeFull { get; set; } = "";
    public bool IsCompositeKey { get; set; }
    public List<string> CompositeKeyPropertyNames { get; set; } = new List<string>();
    public string DbContextTypeFull { get; set; } = "";

    /// <summary>
    /// The developer's project-level repository base (open generic with entity substituted).
    /// e.g., "global::MyApp.MyProjectRepository&lt;global::MyApp.Domain.Order&gt;"
    /// If null, falls back to "global::FrenchExDev.Net.Entity.Dsl.Abstractions.RepositoryBase&lt;{EntityClassFull}&gt;"
    /// </summary>
    public string? RepositoryBaseTypeFull { get; set; }
}
