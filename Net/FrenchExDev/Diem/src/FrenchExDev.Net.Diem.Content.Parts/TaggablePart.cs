namespace FrenchExDev.Net.Diem.Content.Parts;

using FrenchExDev.Net.Diem.Content.Parts.Attributes;

[ContentPart("Taggable", Description = "Taxonomy tagging")]
public partial class TaggablePart
{
    [PartField("Tags", DisplayName = "Tags", HelpText = "Comma-separated tag list")]
    public string Tags { get; set; } = "";

    [PartField("Taxonomy", DisplayName = "Taxonomy")]
    public string Taxonomy { get; set; } = "";
}
