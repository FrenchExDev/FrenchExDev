namespace FrenchExDev.Net.Diem.Content.Parts;

using FrenchExDev.Net.Diem.Content.Parts.Attributes;

[ContentPart("Seoable", Description = "SEO metadata")]
public partial class SeoablePart
{
    [PartField("MetaTitle", DisplayName = "Meta Title", MaxLength = 70)]
    public string? MetaTitle { get; set; }

    [PartField("MetaDescription", DisplayName = "Meta Description", MaxLength = 160)]
    public string? MetaDescription { get; set; }

    [PartField("OgImage", DisplayName = "Open Graph Image")]
    public string? OgImage { get; set; }

    [PartField("NoIndex", DisplayName = "No Index")]
    public bool NoIndex { get; set; }
}
