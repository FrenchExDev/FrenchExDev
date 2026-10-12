namespace FrenchExDev.Net.Diem.Content.Blocks;

using FrenchExDev.Net.Diem.Content.Blocks.Attributes;

[StructBlock("Testimonial", Description = "Customer testimonial block", Icon = "quote")]
public partial class TestimonialBlock : IContentBlock
{
    public string BlockType => "Testimonial";

    [BlockField("Quote", Required = true)]
    public string Quote { get; set; } = "";

    [BlockField("Author", Required = true)]
    public string Author { get; set; } = "";

    [BlockField("Role", DisplayName = "Author Role")]
    public string? Role { get; set; }

    [BlockField("AvatarUrl", DisplayName = "Avatar URL")]
    public string? AvatarUrl { get; set; }
}
