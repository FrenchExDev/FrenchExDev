namespace FrenchExDev.Net.Diem.Content.Parts;

using FrenchExDev.Net.Diem.Content.Parts.Attributes;

[ContentPart("Routable", Description = "URL routing with slugs")]
public partial class RoutablePart
{
    [PartField("Slug", Required = true, MaxLength = 200, HelpText = "URL-friendly identifier")]
    public string Slug { get; set; } = "";

    [PartField("UrlPath", DisplayName = "URL Path")]
    public string UrlPath { get; set; } = "";

    [PartField("IsCanonical", DisplayName = "Canonical URL")]
    public bool IsCanonical { get; set; }
}
