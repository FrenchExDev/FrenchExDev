namespace FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

using System.Collections.Generic;

public sealed class DbContextEmitModel
{
    public string Namespace { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string? BoundedContext { get; set; }
    public List<DbSetModel> DbSets { get; set; } = new List<DbSetModel>();

    /// <summary>
    /// Developer's project-level repository base (open generic, fully qualified WITHOUT type parameter).
    /// e.g., "global::MyApp.MyProjectRepository" — the SG appends "&lt;EntityType&gt;" per entity.
    /// If null, repositories inherit RepositoryBase&lt;T&gt; from Abstractions directly.
    /// </summary>
    public string? RepositoryBaseTypeFull { get; set; }
}
