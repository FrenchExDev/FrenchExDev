namespace FrenchExDev.Net.Diem.Content.Parts;

using FrenchExDev.Net.Diem.Content.Parts.Attributes;

[ContentPart("Versionable", Description = "Temporal data versioning")]
public partial class VersionablePart
{
    [PartField("VersionNumber")]
    public int VersionNumber { get; set; } = 1;

    [PartField("ValidFrom")]
    public DateTimeOffset ValidFrom { get; set; }

    [PartField("ValidTo")]
    public DateTimeOffset? ValidTo { get; set; }

    [PartField("IsCurrent")]
    public bool IsCurrent { get; set; } = true;
}
