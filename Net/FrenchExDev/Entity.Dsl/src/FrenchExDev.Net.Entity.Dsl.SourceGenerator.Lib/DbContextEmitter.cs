namespace FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

using System.Text;

/// <summary>
/// Emits the Generation Gap files for DbContext:
/// 1. {Context}Base.g.cs — abstract DbContext with hooks + lifecycle dispatch (always regenerated)
/// 2. {Context}.g.cs — partial stub (always regenerated, developer extends via second partial)
/// </summary>
public static class DbContextEmitter
{
    /// <summary>Emits the abstract base DbContext with DbSets, OnModelCreating, SaveChanges hooks.</summary>
    public static string EmitBase(DbContextEmitModel model)
    {
        var sb = new StringBuilder(4096);
        EmitFileHeader(sb);
        sb.AppendLine($"namespace {model.Namespace};");
        sb.AppendLine();
        sb.AppendLine("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
        sb.AppendLine($"public abstract class {model.ClassName}Base : Microsoft.EntityFrameworkCore.DbContext");
        sb.AppendLine("{");

        // Constructor
        sb.AppendLine($"    protected {model.ClassName}Base(");
        sb.AppendLine("        Microsoft.EntityFrameworkCore.DbContextOptions options,");
        sb.AppendLine("        global::System.IServiceProvider? serviceProvider = null)");
        sb.AppendLine("        : base(options)");
        sb.AppendLine("    {");
        sb.AppendLine("        _serviceProvider = serviceProvider;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private readonly global::System.IServiceProvider? _serviceProvider;");
        sb.AppendLine();

        // DbSets
        foreach (var dbSet in model.DbSets)
        {
            if (dbSet.IsKeyless) continue;
            sb.AppendLine($"    public Microsoft.EntityFrameworkCore.DbSet<{dbSet.EntityTypeFull}> {dbSet.PropertyName} {{ get; set; }} = null!;");
        }
        sb.AppendLine();

        // Hooks
        sb.AppendLine("    protected virtual void PreModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder) { }");
        sb.AppendLine("    protected virtual void PostModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder) { }");
        sb.AppendLine();

        // RegisterConfigurations
        sb.AppendLine("    protected virtual void RegisterConfigurations(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)");
        sb.AppendLine("    {");
        foreach (var dbSet in model.DbSets)
        {
            sb.AppendLine($"        modelBuilder.ApplyConfiguration(new {dbSet.EntityTypeFull.Replace("global::", "global::")}Configuration.{GetClassName(dbSet.EntityTypeFull)}ConfigurationRegistration());");
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        // OnModelCreating (sealed)
        sb.AppendLine("    protected sealed override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)");
        sb.AppendLine("    {");
        sb.AppendLine("        base.OnModelCreating(modelBuilder);");
        sb.AppendLine("        PreModelCreating(modelBuilder);");
        sb.AppendLine("        RegisterConfigurations(modelBuilder);");
        sb.AppendLine("        PostModelCreating(modelBuilder);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Lifecycle hooks
        sb.AppendLine("    protected virtual void OnEntitiesAdding(");
        sb.AppendLine("        global::System.Collections.Generic.IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> entries) { }");
        sb.AppendLine("    protected virtual void OnEntitiesModifying(");
        sb.AppendLine("        global::System.Collections.Generic.IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> entries) { }");
        sb.AppendLine("    protected virtual void OnEntitiesDeleting(");
        sb.AppendLine("        global::System.Collections.Generic.IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> entries) { }");
        sb.AppendLine();

        // SaveChanges overrides
        sb.AppendLine("    public override int SaveChanges(bool acceptAllChangesOnSuccess)");
        sb.AppendLine("    {");
        sb.AppendLine("        OnBeforeSaveChanges();");
        sb.AppendLine("        return base.SaveChanges(acceptAllChangesOnSuccess);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override global::System.Threading.Tasks.Task<int> SaveChangesAsync(");
        sb.AppendLine("        bool acceptAllChangesOnSuccess,");
        sb.AppendLine("        global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        OnBeforeSaveChanges();");
        sb.AppendLine("        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // OnBeforeSaveChanges
        sb.AppendLine("    private void OnBeforeSaveChanges()");
        sb.AppendLine("    {");
        sb.AppendLine("        var entries = ChangeTracker.Entries().ToList();");
        sb.AppendLine("        OnEntitiesAdding(entries.Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added));");
        sb.AppendLine("        OnEntitiesModifying(entries.Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Modified));");
        sb.AppendLine("        OnEntitiesDeleting(entries.Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Deleted));");
        sb.AppendLine("    }");

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>Emits the partial class stub extending Base.</summary>
    public static string EmitPartialStub(DbContextEmitModel model)
    {
        var sb = new StringBuilder(512);
        EmitFileHeader(sb);
        sb.AppendLine($"namespace {model.Namespace};");
        sb.AppendLine();
        sb.AppendLine($"public partial class {model.ClassName} : {model.ClassName}Base");
        sb.AppendLine("{");
        sb.AppendLine($"    public {model.ClassName}(");
        sb.AppendLine($"        Microsoft.EntityFrameworkCore.DbContextOptions<{model.ClassName}> options,");
        sb.AppendLine("        global::System.IServiceProvider? serviceProvider = null)");
        sb.AppendLine("        : base(options, serviceProvider) { }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using global::System.Linq;");
        sb.AppendLine();
    }

    private static string GetClassName(string fullName)
    {
        var idx = fullName.LastIndexOf('.');
        return idx >= 0 ? fullName.Substring(idx + 1) : fullName;
    }
}
