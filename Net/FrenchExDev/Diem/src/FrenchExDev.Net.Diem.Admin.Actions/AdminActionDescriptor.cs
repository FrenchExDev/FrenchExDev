namespace FrenchExDev.Net.Diem.Admin.Actions;

public class AdminActionDescriptor
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public string? Icon { get; init; }
    public string? ConfirmationMessage { get; init; }
    public string? RequiresRole { get; init; }
}
