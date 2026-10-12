namespace FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;

using System.Text;

/// <summary>
/// Emits the Generation Gap files for UnitOfWork:
/// 1. I{Context}UnitOfWork.g.cs — interface with all repository properties (always regenerated)
/// 2. {Context}UnitOfWorkBase.g.cs — abstract base with virtual factory methods (always regenerated)
/// 3. {Context}UnitOfWork.g.cs — partial stub with [Injectable] (always regenerated)
/// </summary>
public static class UnitOfWorkEmitter
{
    public static string EmitInterface(UnitOfWorkEmitModel model)
    {
        var sb = new StringBuilder(512);
        EmitFileHeader(sb);
        sb.AppendLine($"namespace {model.Namespace};");
        sb.AppendLine();
        sb.AppendLine($"public interface I{model.DbContextClassName}UnitOfWork : global::FrenchExDev.Net.Entity.Dsl.Abstractions.IUnitOfWork<{model.DbContextClassFull}>");
        sb.AppendLine("{");
        foreach (var repo in model.Repositories)
        {
            sb.AppendLine($"    {repo.InterfaceTypeFull} {repo.PropertyName} {{ get; }}");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string EmitBase(UnitOfWorkEmitModel model)
    {
        var sb = new StringBuilder(2048);
        EmitFileHeader(sb);
        sb.AppendLine($"namespace {model.Namespace};");
        sb.AppendLine();
        sb.AppendLine("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
        sb.AppendLine($"public abstract class {model.DbContextClassName}UnitOfWorkBase : I{model.DbContextClassName}UnitOfWork");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {model.DbContextClassFull} _context;");
        sb.AppendLine();

        // Lazy fields
        foreach (var repo in model.Repositories)
        {
            var fieldName = "_" + char.ToLowerInvariant(repo.PropertyName[0]) + repo.PropertyName.Substring(1);
            sb.AppendLine($"    private {repo.InterfaceTypeFull}? {fieldName};");
        }
        sb.AppendLine();

        // Constructor
        sb.AppendLine($"    protected {model.DbContextClassName}UnitOfWorkBase({model.DbContextClassFull} context)");
        sb.AppendLine("    {");
        sb.AppendLine("        _context = context;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Context property
        sb.AppendLine($"    public {model.DbContextClassFull} Context => _context;");
        sb.AppendLine();

        // Repository properties (lazy)
        foreach (var repo in model.Repositories)
        {
            var fieldName = "_" + char.ToLowerInvariant(repo.PropertyName[0]) + repo.PropertyName.Substring(1);
            var factoryName = "Create" + repo.PropertyName.TrimEnd('s') + "Repository";
            // Simpler: use property name directly
            sb.AppendLine($"    public {repo.InterfaceTypeFull} {repo.PropertyName}");
            sb.AppendLine($"        => {fieldName} ??= Create{repo.PropertyName}Repository();");
            sb.AppendLine();
        }

        // Factory methods (virtual)
        foreach (var repo in model.Repositories)
        {
            sb.AppendLine($"    protected virtual {repo.InterfaceTypeFull} Create{repo.PropertyName}Repository()");
            sb.AppendLine($"        => new {repo.ImplementationTypeFull}(_context);");
            sb.AppendLine();
        }

        // IUnitOfWork
        sb.AppendLine("    public virtual global::System.Threading.Tasks.Task<int> SaveChangesAsync(global::System.Threading.CancellationToken ct = default)");
        sb.AppendLine("        => _context.SaveChangesAsync(ct);");
        sb.AppendLine();
        sb.AppendLine("    public virtual int SaveChanges()");
        sb.AppendLine("        => _context.SaveChanges();");
        sb.AppendLine();
        sb.AppendLine("    public virtual async global::System.Threading.Tasks.Task<global::FrenchExDev.Net.Entity.Dsl.Abstractions.IUnitOfWorkTransaction> BeginTransactionAsync(global::System.Threading.CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        var tx = await _context.Database.BeginTransactionAsync(ct);");
        sb.AppendLine("        return new UnitOfWorkTransaction(tx);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public virtual void DetachAll() => _context.ChangeTracker.Clear();");
        sb.AppendLine("    public virtual bool HasChanges => _context.ChangeTracker.HasChanges();");
        sb.AppendLine();
        sb.AppendLine("    public void Dispose() => _context.Dispose();");
        sb.AppendLine("    public global::System.Threading.Tasks.ValueTask DisposeAsync() => _context.DisposeAsync();");
        sb.AppendLine();

        // Transaction wrapper
        sb.AppendLine("    private sealed class UnitOfWorkTransaction : global::FrenchExDev.Net.Entity.Dsl.Abstractions.IUnitOfWorkTransaction");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _tx;");
        sb.AppendLine("        public UnitOfWorkTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx) => _tx = tx;");
        sb.AppendLine("        public global::System.Threading.Tasks.Task CommitAsync(global::System.Threading.CancellationToken ct) => _tx.CommitAsync(ct);");
        sb.AppendLine("        public global::System.Threading.Tasks.Task RollbackAsync(global::System.Threading.CancellationToken ct) => _tx.RollbackAsync(ct);");
        sb.AppendLine("        public global::System.Threading.Tasks.ValueTask DisposeAsync() => _tx.DisposeAsync();");
        sb.AppendLine("    }");

        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string EmitPartialStub(UnitOfWorkEmitModel model)
    {
        var sb = new StringBuilder(512);
        EmitFileHeader(sb);
        sb.AppendLine($"namespace {model.Namespace};");
        sb.AppendLine();
        sb.AppendLine("[global::FrenchExDev.Net.Injectable.Attributes.Injectable(");
        sb.AppendLine($"    Scope = global::FrenchExDev.Net.Injectable.Attributes.Scope.Scoped,");
        sb.AppendLine($"    As = typeof(I{model.DbContextClassName}UnitOfWork))]");
        sb.AppendLine($"public partial class {model.DbContextClassName}UnitOfWork : {model.DbContextClassName}UnitOfWorkBase");
        sb.AppendLine("{");
        sb.AppendLine($"    public {model.DbContextClassName}UnitOfWork({model.DbContextClassFull} context) : base(context) {{ }}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
    }
}
