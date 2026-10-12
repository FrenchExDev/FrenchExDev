namespace FrenchExDev.Net.Packer.Bundle.Hcl2;

/// <summary>
/// Validation rule for a <see cref="PackerVariable"/>.
/// </summary>
public sealed record PackerVariableValidation(string Condition, string ErrorMessage);

/// <summary>
/// Models a <c>variable "name" { }</c> block with type, default, description, sensitive, and validation.
/// Immutable record.
/// </summary>
public sealed record PackerVariable
{
    public required string Name { get; init; }
    public string? Type { get; init; }
    public object? Default { get; init; }
    public string? Description { get; init; }
    public bool? Sensitive { get; init; }
    public PackerVariableValidation? Validation { get; init; }

    /// <summary>Writes this variable as HCL2 to the given writer.</summary>
    public void WriteTo(HclWriter writer)
    {
        using var block = writer.Block("variable", Name);

        if (Type is not null)
            writer.Expression("type", Type);

        if (Default is not null)
        {
            switch (Default)
            {
                case string s:
                    writer.Argument("default", s);
                    break;
                case int i:
                    writer.Argument("default", i);
                    break;
                case bool b:
                    writer.Argument("default", b);
                    break;
                default:
                    writer.Argument("default", Default.ToString());
                    break;
            }
        }

        writer.Argument("description", Description);

        if (Sensitive is true)
            writer.Argument("sensitive", true);

        if (Validation is not null)
        {
            writer.BlankLine();
            using var val = writer.Block("validation");
            writer.Expression("condition", Validation.Condition);
            writer.Argument("error_message", Validation.ErrorMessage);
        }
    }
}
