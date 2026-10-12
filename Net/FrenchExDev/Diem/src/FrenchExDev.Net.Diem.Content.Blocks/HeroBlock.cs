namespace FrenchExDev.Net.Diem.Content.Blocks;

using FrenchExDev.Net.Diem.Content.Blocks.Attributes;

[StructBlock("Hero", Description = "Hero banner with heading and call-to-action", Icon = "star")]
public partial class HeroBlock : IContentBlock
{
    public string BlockType => "Hero";

    [BlockField("Heading", Required = true, MaxLength = 120)]
    public string Heading { get; set; } = "";

    [BlockField("Subheading", DisplayName = "Sub-heading")]
    public string? Subheading { get; set; }

    [BlockField("BackgroundImage", DisplayName = "Background Image")]
    public string? BackgroundImage { get; set; }

    [BlockField("CtaText", DisplayName = "CTA Text")]
    public string? CtaText { get; set; }

    [BlockField("CtaUrl", DisplayName = "CTA URL")]
    public string? CtaUrl { get; set; }
}
