namespace FrenchExDev.Net.Diem.Identity;

public sealed class DiemUser
{
    public Guid Id { get; set; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; set; }
    public IList<string> Roles { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public static class DiemRoles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Author = "Author";
    public const string Publisher = "Publisher";
    public const string Viewer = "Viewer";
}

public interface IDiemUserContext
{
    DiemUser? CurrentUser { get; }
    bool IsInRole(string role);
    string? UserId { get; }
}
