using System.Text;

namespace FrenchExDev.Net.Packer.Bundle;

/// <summary>
/// A .env.template variable definition.
/// </summary>
public sealed class EnvVariable
{
    public required string Key { get; set; }
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
}

/// <summary>
/// Models a .env.template file — variable definitions with defaults and descriptions.
/// Mutable — contributors add variables.
/// </summary>
public sealed class EnvTemplate
{
    public List<EnvVariable> Variables { get; set; } = new();

    /// <summary>Renders the template as a string: <c># description\nKEY=default</c></summary>
    public string Render()
    {
        var sb = new StringBuilder();
        foreach (var v in Variables)
        {
            if (v.Description is not null)
                sb.AppendLine($"# {v.Description}");
            sb.AppendLine($"{v.Key}={v.DefaultValue ?? ""}");
        }
        return sb.ToString();
    }
}
