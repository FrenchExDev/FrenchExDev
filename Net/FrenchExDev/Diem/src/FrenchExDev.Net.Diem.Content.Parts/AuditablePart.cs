namespace FrenchExDev.Net.Diem.Content.Parts;

using FrenchExDev.Net.Diem.Content.Parts.Attributes;

[ContentPart("Auditable", Description = "Audit trail tracking")]
public partial class AuditablePart
{
    [PartField("CreatedBy", DisplayName = "Created By")]
    public string CreatedBy { get; set; } = "";

    [PartField("CreatedAt", DisplayName = "Created At")]
    public DateTimeOffset CreatedAt { get; set; }

    [PartField("ModifiedBy", DisplayName = "Modified By")]
    public string ModifiedBy { get; set; } = "";

    [PartField("ModifiedAt", DisplayName = "Modified At")]
    public DateTimeOffset ModifiedAt { get; set; }
}
