namespace FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

using System.Collections.Generic;

public sealed class UnitOfWorkEmitModel
{
    public string Namespace { get; set; } = "";
    public string DbContextClassName { get; set; } = "";
    public string DbContextClassFull { get; set; } = "";
    public List<UnitOfWorkRepositoryModel> Repositories { get; set; } = new List<UnitOfWorkRepositoryModel>();
}

public sealed class UnitOfWorkRepositoryModel
{
    public string InterfaceTypeFull { get; set; } = "";
    public string ImplementationTypeFull { get; set; } = "";
    public string PropertyName { get; set; } = "";
}
