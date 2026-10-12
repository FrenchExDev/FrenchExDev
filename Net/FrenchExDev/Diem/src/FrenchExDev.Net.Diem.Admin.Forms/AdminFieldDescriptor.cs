namespace FrenchExDev.Net.Diem.Admin.Forms;

public class AdminFieldDescriptor
{
    public required string Name { get; init; }
    public string? DisplayType { get; init; }
    public string? DisplayName { get; init; }
    public bool ReadOnly { get; init; }
    public bool HideInList { get; init; }
    public bool HideInForm { get; init; }
    public int Order { get; init; }
}
