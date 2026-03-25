using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FrenchExDev.Net.Builder.SourceGenerator.Lib;

namespace FrenchExDev.Net.BinaryWrapper.SourceGenerator;

/// <summary>
/// Emits {Command}Builder.g.cs — delegates to <see cref="BuilderEmitter"/> with BinaryWrapper-specific
/// extensions (version guards, SemanticVersion constructor, AsReadOnly conversion).
/// </summary>
internal static class BuilderClassEmitter
{
    public static string Emit(DescriptorModel descriptor, UnifiedCommand cmd)
    {
        var (resolvedOptions, resolvedArguments) = NamingHelper.ResolvePropertyNames(cmd.Options, cmd.Arguments);
        cmd = new UnifiedCommand
        {
            CommandPath = cmd.CommandPath, PathSegments = cmd.PathSegments, Name = cmd.Name,
            Description = cmd.Description, SinceVersion = cmd.SinceVersion, UntilVersion = cmd.UntilVersion,
            Options = resolvedOptions, Arguments = resolvedArguments
        };

        var commandClassName = NamingHelper.CommandClassName(descriptor.BinaryName, cmd);
        var builderClassName = NamingHelper.BuilderClassName(descriptor.BinaryName, cmd);
        var ns = descriptor.Namespace;
        var commandPath = cmd.PathSegments.Count > 1
            ? string.Join(".", cmd.PathSegments.Skip(1))
            : cmd.Name;

        // Build property models from options + arguments
        var properties = new List<BuilderPropertyModel>();

        foreach (var opt in cmd.Options)
        {
            var propName = NamingHelper.OptionPropertyName(opt);
            var clrType = NamingHelper.MapClrType(opt.ClrType);
            var isMultiple = string.Equals(opt.ValueKind, "multiple", StringComparison.OrdinalIgnoreCase);

            var typeFull = isMultiple
                ? $"global::System.Collections.Generic.List<{clrType}>?"
                : NamingHelper.NullableType(clrType);

            // Version attributes for With method
            var withAttrs = BuildVersionAttributes(opt.SinceVersion, opt.UntilVersion);

            // Version guard body prefix
            var bodyPrefix = BuildVersionGuard(opt, commandPath);

            // AsReadOnly for collections in CreateInstance
            var instantExpr = isMultiple ? $"{propName}?.AsReadOnly()" : null;

            properties.Add(new BuilderPropertyModel(
                propName, typeFull, typeFull,
                isCollection: isMultiple, itemTypeFull: isMultiple ? clrType : null,
                withMethodAttributes: withAttrs,
                withMethodBodyPrefix: bodyPrefix,
                instantiationExpression: instantExpr));
        }

        foreach (var arg in cmd.Arguments)
        {
            var propName = NamingHelper.ArgumentPropertyName(arg);
            var clrType = NamingHelper.MapClrType(arg.ClrType);
            var isVariadic = arg.IsVariadic;

            var typeFull = isVariadic
                ? $"global::System.Collections.Generic.List<{clrType}>?"
                : NamingHelper.NullableType(clrType);

            var instantExpr = isVariadic ? $"{propName}?.AsReadOnly()" : null;

            properties.Add(new BuilderPropertyModel(
                propName, typeFull, typeFull,
                isCollection: isVariadic, itemTypeFull: isVariadic ? clrType : null,
                instantiationExpression: instantExpr));
        }

        // Preamble: _detectedVersion field + constructor
        var preamble = new StringBuilder();
        preamble.AppendLine($"    private readonly global::FrenchExDev.Net.BinaryWrapper.SemanticVersion? _detectedVersion;");
        preamble.AppendLine();
        preamble.AppendLine($"    public {builderClassName}(global::FrenchExDev.Net.BinaryWrapper.SemanticVersion? detectedVersion = null)");
        preamble.AppendLine("    {");
        preamble.AppendLine("        _detectedVersion = detectedVersion;");
        preamble.Append("    }");

        var model = new BuilderEmitModel(
            ns, commandClassName, builderClassName, properties,
            preamble: preamble.ToString());

        return BuilderEmitter.Emit(model);
    }

    private static List<string>? BuildVersionAttributes(string? sinceVersion, string? untilVersion)
    {
        if (sinceVersion is null && untilVersion is null)
            return null;

        var attrs = new List<string>();
        if (sinceVersion is not null)
            attrs.Add($"[global::FrenchExDev.Net.BinaryWrapper.SinceVersion(\"{sinceVersion}\")]");
        if (untilVersion is not null)
            attrs.Add($"[global::FrenchExDev.Net.BinaryWrapper.UntilVersion(\"{untilVersion}\")]");
        return attrs;
    }

    private static string? BuildVersionGuard(UnifiedOption opt, string commandPath)
    {
        if (opt.SinceVersion is null && opt.UntilVersion is null)
            return null;

        var sb = new StringBuilder();
        sb.Append("global::FrenchExDev.Net.BinaryWrapper.VersionGuard.EnsureOptionSupported(");
        sb.Append($"_detectedVersion, \"{NamingHelper.EscapeString(commandPath)}\", \"{NamingHelper.EscapeString(opt.LongName)}\", ");
        sb.Append(opt.SinceVersion is not null ? NamingHelper.SemanticVersionCtor(opt.SinceVersion) : "null");
        sb.Append(", ");
        sb.Append(opt.UntilVersion is not null ? NamingHelper.SemanticVersionCtor(opt.UntilVersion) : "null");
        sb.Append(");");
        return sb.ToString();
    }
}
