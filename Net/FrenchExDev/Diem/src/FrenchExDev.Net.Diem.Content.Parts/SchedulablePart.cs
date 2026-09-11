namespace FrenchExDev.Net.Diem.Content.Parts;

using FrenchExDev.Net.Diem.Content.Parts.Attributes;

[ContentPart("Schedulable", Description = "Publication scheduling")]
public partial class SchedulablePart
{
    [PartField("PublishAt", DisplayName = "Publish At")]
    public DateTimeOffset? PublishAt { get; set; }

    [PartField("UnpublishAt", DisplayName = "Unpublish At")]
    public DateTimeOffset? UnpublishAt { get; set; }

    [PartField("IsPublished", DisplayName = "Published")]
    public bool IsPublished { get; set; }
}
