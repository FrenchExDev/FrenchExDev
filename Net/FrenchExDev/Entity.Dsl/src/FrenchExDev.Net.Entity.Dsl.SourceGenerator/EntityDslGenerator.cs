namespace FrenchExDev.Net.Entity.Dsl.SourceGenerator;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

[Generator]
public sealed class EntityDslGenerator : IIncrementalGenerator
{
    private const string MappedEntityAttributeFqn = "FrenchExDev.Net.Entity.Dsl.Attributes.MappedEntityAttribute";
    private const string TableAttributeFqn = "FrenchExDev.Net.Entity.Dsl.Attributes.TableAttribute";
    private const string ColumnAttributeFqn = "FrenchExDev.Net.Entity.Dsl.Attributes.ColumnAttribute";
    private const string PrimaryKeyAttributeFqn = "FrenchExDev.Net.Entity.Dsl.Attributes.PrimaryKeyAttribute";
    private const string DbContextAttributeFqn = "FrenchExDev.Net.Entity.Dsl.Attributes.DbContextAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Discover entities from [MappedEntity] (Entity.Dsl's own entry point)
        var allEntities = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MappedEntityAttributeFqn,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => ExtractEntityModel(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        // 4. Emit per-entity: ConfigurationBase, Configuration, ConfigurationRegistration
        context.RegisterSourceOutput(allEntities, static (spc, model) =>
        {
            spc.AddSource($"{model.ClassName}ConfigurationBase.g.cs",
                EntityConfigurationEmitter.EmitBase(model));
            spc.AddSource($"{model.ClassName}Configuration.g.cs",
                EntityConfigurationEmitter.EmitPartialStub(model));
            spc.AddSource($"{model.ClassName}ConfigurationRegistration.g.cs",
                EntityConfigurationEmitter.EmitRegistration(model));
        });

        // 5. Discover DbContexts
        var dbContexts = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                DbContextAttributeFqn,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => ExtractDbContextModel(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        // 6. Emit DbContext files
        var dbContextWithEntities = dbContexts.Combine(allEntities.Collect());
        context.RegisterSourceOutput(dbContextWithEntities, static (spc, pair) =>
        {
            var (dbCtx, ents) = pair;

            // Populate DbSets from collected entities
            var enriched = new DbContextEmitModel
            {
                Namespace = dbCtx.Namespace,
                ClassName = dbCtx.ClassName,
                BoundedContext = dbCtx.BoundedContext,
                DbSets = ents
                    .Select(e => new DbSetModel
                    {
                        EntityTypeFull = e.ClassFullName,
                        PropertyName = NamingHelper.Pluralize(e.ClassName)
                    })
                    .ToList()
            };

            spc.AddSource($"{dbCtx.ClassName}Base.g.cs",
                DbContextEmitter.EmitBase(enriched));
            spc.AddSource($"{dbCtx.ClassName}.g.cs",
                DbContextEmitter.EmitPartialStub(enriched));
            spc.AddSource($"{dbCtx.ClassName}Registration.g.cs",
                DbContextRegistrationEmitter.Emit(enriched));

            // UnitOfWork
            var uowModel = new UnitOfWorkEmitModel
            {
                Namespace = dbCtx.Namespace,
                DbContextClassName = dbCtx.ClassName,
                DbContextClassFull = $"global::{dbCtx.Namespace}.{dbCtx.ClassName}",
                Repositories = ents
                    .Select(e => new UnitOfWorkRepositoryModel
                    {
                        InterfaceTypeFull = $"global::{e.Namespace}.Repositories.I{e.ClassName}Repository",
                        ImplementationTypeFull = $"global::{e.Namespace}.Repositories.{e.ClassName}Repository",
                        PropertyName = NamingHelper.Pluralize(e.ClassName)
                    })
                    .ToList()
            };

            spc.AddSource($"I{dbCtx.ClassName}UnitOfWork.g.cs",
                UnitOfWorkEmitter.EmitInterface(uowModel));
            spc.AddSource($"{dbCtx.ClassName}UnitOfWorkBase.g.cs",
                UnitOfWorkEmitter.EmitBase(uowModel));
            spc.AddSource($"{dbCtx.ClassName}UnitOfWork.g.cs",
                UnitOfWorkEmitter.EmitPartialStub(uowModel));

            // Repository files per entity
            foreach (var ent in ents)
            {
                var repoModel = new RepositoryEmitModel
                {
                    Namespace = ent.Namespace,
                    EntityClassName = ent.ClassName,
                    EntityClassFull = ent.ClassFullName,
                    PrimaryKeyTypeFull = GetPrimaryKeyType(ent),
                    IsCompositeKey = ent.PrimaryKeyProperties.Count > 1,
                    CompositeKeyPropertyNames = ent.PrimaryKeyProperties
                        .OrderBy(k => k.Order)
                        .Select(k => k.PropertyName)
                        .ToList(),
                    DbContextTypeFull = $"global::{dbCtx.Namespace}.{dbCtx.ClassName}",
                    RepositoryBaseTypeFull = dbCtx.RepositoryBaseTypeFull != null
                        ? $"{dbCtx.RepositoryBaseTypeFull}<{ent.ClassFullName}>"
                        : null
                };

                spc.AddSource($"I{ent.ClassName}Repository.g.cs",
                    RepositoryEmitter.EmitInterface(repoModel));
                spc.AddSource($"{ent.ClassName}Repository.g.cs",
                    RepositoryEmitter.EmitPartialStub(repoModel));
            }
        });
    }

    private static EntityEmitModel? ExtractEntityModel(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var model = new EntityEmitModel
        {
            Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
            ClassName = typeSymbol.Name,
            ClassFullName = $"global::{typeSymbol.ToDisplayString()}"
        };

        // Read [Table] attribute
        var tableAttr = typeSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == TableAttributeFqn);
        if (tableAttr != null)
        {
            foreach (var named in tableAttr.NamedArguments)
            {
                if (named.Key == "Name" && named.Value.Value is string tableName)
                    model.TableName = tableName;
                if (named.Key == "Schema" && named.Value.Value is string schema)
                    model.Schema = schema;
            }
        }

        // Read properties
        foreach (var member in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            ct.ThrowIfCancellationRequested();

            if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public)
                continue;

            var attrs = member.GetAttributes();

            // Check [PrimaryKey]
            var pkAttr = attrs.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == PrimaryKeyAttributeFqn);
            if (pkAttr != null)
            {
                var keyModel = new KeyPropertyModel { PropertyName = member.Name };
                foreach (var named in pkAttr.NamedArguments)
                {
                    if (named.Key == "Order" && named.Value.Value is int order)
                        keyModel.Order = order;
                    if (named.Key == "ValueGenerated" && named.Value.Value is int vg)
                        keyModel.ValueGenerated = ((ValueGenerationEnum)vg).ToString();
                }
                model.PrimaryKeyProperties.Add(keyModel);
                continue; // PK properties are handled separately from regular properties
            }

            // Check [Column]
            var colAttr = attrs.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == ColumnAttributeFqn);

            var propModel = new PropertyConfigModel { PropertyName = member.Name };

            if (colAttr != null)
            {
                foreach (var named in colAttr.NamedArguments)
                {
                    if (named.Key == "Name" && named.Value.Value is string colName)
                        propModel.ColumnName = colName;
                    if (named.Key == "TypeName" && named.Value.Value is string typeName)
                        propModel.ColumnType = typeName;
                    if (named.Key == "Order" && named.Value.Value is int colOrder)
                        propModel.ColumnOrder = colOrder;
                }
            }

            // No DDD [Property] reading — Entity.Dsl is standalone.
            // [Required] and [MaxLength] from Entity.Dsl's own attributes (Phase 2).

            model.Properties.Add(propModel);
        }

        return model;
    }

    private static DbContextEmitModel? ExtractDbContextModel(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var model = new DbContextEmitModel
        {
            Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
            ClassName = typeSymbol.Name
        };

        var dbCtxAttr = typeSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DbContextAttributeFqn);
        if (dbCtxAttr != null)
        {
            foreach (var named in dbCtxAttr.NamedArguments)
            {
                if (named.Key == "BoundedContext" && named.Value.Value is string bc)
                    model.BoundedContext = bc;
                if (named.Key == "RepositoryBase" && named.Value.Value is INamedTypeSymbol repoBaseType)
                    model.RepositoryBaseTypeFull = repoBaseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
        }

        return model;
    }

    private static string GetPrimaryKeyType(EntityEmitModel model)
    {
        if (model.PrimaryKeyProperties.Count == 0)
            return "object";
        // For Phase 1, we use object — the typed overload uses the actual type from Roslyn
        // which we'd need to pass through. For now, object works.
        return "object";
    }

    // Mirror of the ValueGeneration enum from Attributes (SG can't reference Attributes assembly)
    private enum ValueGenerationEnum
    {
        None = 0,
        OnAdd = 1,
        OnUpdate = 2,
        OnAddOrUpdate = 3
    }
}
