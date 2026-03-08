using System.Text;
using DockAi.Api.Dsl;
using DockAi.Api.Models;

namespace DockAi.Api.Ai;

/// <summary>
/// Converts an OntologySchema back to valid DSL text.
/// Reverse of DslSchemaParser: OntologySchema → .dsl file content.
/// </summary>
public static class DslEmitter
{
    public static string Emit(OntologySchema schema)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Auto-generated ontology by DockAi AI");
        sb.AppendLine();

        // Entity types
        if (schema.EntityTypes.Count > 0)
        {
            sb.AppendLine("# ── Entity Types ──");
            sb.AppendLine();
            foreach (var (id, type) in schema.EntityTypes)
            {
                sb.AppendLine($"@type {id} {{");
                foreach (var (_, prop) in type.Properties)
                    sb.AppendLine($"    {prop.Name}: {FormatKind(prop)}");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        // Entities
        if (schema.Entities.Count > 0)
        {
            sb.AppendLine("# ── Entities ──");
            sb.AppendLine();
            foreach (var (id, entity) in schema.Entities)
            {
                sb.AppendLine($"@entity {id} : {entity.TypeId} {{");
                foreach (var (key, val) in entity.Properties)
                    sb.AppendLine($"    {key}: {FormatValue(val)}");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        // Relation types
        if (schema.RelationTypes.Count > 0)
        {
            sb.AppendLine("# ── Relation Types ──");
            sb.AppendLine();
            foreach (var (id, rt) in schema.RelationTypes)
            {
                sb.AppendLine($"@relation.type {id} {{");
                sb.AppendLine($"    from: {rt.FromType}");
                sb.AppendLine($"    to: [{string.Join(", ", rt.ToTypes)}]");
                if (rt.PropertyNames.Count > 0)
                    sb.AppendLine($"    properties: [{string.Join(", ", rt.PropertyNames)}]");
                if (rt.Inverse is not null)
                    sb.AppendLine($"    inverse: {rt.Inverse}");
                if (rt.Symmetric)
                    sb.AppendLine("    symmetric: true");
                if (rt.Auto)
                    sb.AppendLine("    auto: true");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        // Relations
        if (schema.Relations.Count > 0)
        {
            sb.AppendLine("# ── Relations ──");
            sb.AppendLine();
            foreach (var rel in schema.Relations)
            {
                if (rel.Properties.Count > 0)
                {
                    sb.AppendLine($"@relation {rel.FromId} -[{rel.TypeId}]-> {rel.ToId} {{");
                    foreach (var (k, v) in rel.Properties)
                        sb.AppendLine($"    {k}: \"{Escape(v)}\"");
                    sb.AppendLine("}");
                }
                else
                {
                    sb.AppendLine($"@relation {rel.FromId} -[{rel.TypeId}]-> {rel.ToId} {{}}");
                }
            }
            sb.AppendLine();
        }

        // Taxonomies
        if (schema.Taxonomies.Count > 0)
        {
            sb.AppendLine("# ── Taxonomies ──");
            sb.AppendLine();
            foreach (var (id, tax) in schema.Taxonomies)
            {
                sb.AppendLine($"@taxonomy {id} {{");
                sb.AppendLine($"    label: \"{Escape(tax.Label ?? id)}\"");
                sb.AppendLine($"    facet: {(tax.Facet ? "true" : "false")}");
                sb.AppendLine();
                foreach (var node in tax.Roots)
                    EmitNode(sb, node, indent: 4);
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        // Rules
        if (schema.Rules.Count > 0)
        {
            sb.AppendLine("# ── Classification Rules ──");
            sb.AppendLine();
            foreach (var rule in schema.Rules)
            {
                sb.AppendLine($"@rule {rule.Id} {{");
                sb.AppendLine($"    when content matches \"{Escape(rule.Pattern)}\"");
                if (rule.Action == RuleAction.Assign)
                    sb.AppendLine($"    then assign {rule.TaxonomyId}: {rule.NodeId}");
                else
                    sb.AppendLine($"    then extract {rule.ExtractType}");
                sb.AppendLine($"    confidence: {rule.Confidence:F1}");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static void EmitNode(StringBuilder sb, TaxonomyNode node, int indent)
    {
        var pad = new string(' ', indent);
        var attrs = new List<string>();
        if (node.Description is not null) attrs.Add($"desc: \"{Escape(node.Description)}\"");
        if (node.Color is not null) attrs.Add($"color: \"{node.Color}\"");
        if (node.Icon is not null) attrs.Add($"icon: \"{node.Icon}\"");

        var attrStr = attrs.Count > 0 ? " { " + string.Join(", ", attrs) + " }" : "";

        if (node.Children.Count == 0)
        {
            sb.AppendLine($"{pad}{node.Id} \"{Escape(node.Label)}\"{attrStr}");
        }
        else
        {
            sb.AppendLine($"{pad}{node.Id} \"{Escape(node.Label)}\"{attrStr} {{");
            foreach (var child in node.Children)
                EmitNode(sb, child, indent + 4);
            sb.AppendLine($"{pad}}}");
        }
    }

    private static string FormatKind(PropertyDef prop) => prop.Kind switch
    {
        PropertyKind.String => "string",
        PropertyKind.Number => "number",
        PropertyKind.Date => "string",  // dates are strings in DSL
        PropertyKind.Bool => "string",
        PropertyKind.StringArray => "string[]",
        PropertyKind.Enum when prop.EnumValues is { Count: > 0 } =>
            $"enum [{string.Join(", ", prop.EnumValues)}]",
        _ => "string"
    };

    private static string FormatValue(object? val) => val switch
    {
        null => "\"\"",
        string s => $"\"{Escape(s)}\"",
        List<string> list => $"[{string.Join(", ", list.Select(s => $"\"{Escape(s)}\""))}]",
        IEnumerable<object> items => $"[{string.Join(", ", items.Select(i => $"\"{Escape(i?.ToString() ?? "")}\""))}]",
        _ => $"\"{Escape(val.ToString() ?? "")}\"",
    };

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
