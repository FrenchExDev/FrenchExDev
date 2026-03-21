namespace FrenchExDev.Net.Diem.Content.Blocks;

using FrenchExDev.Net.Diem.Content.Blocks.Attributes;

[StructBlock("Image", Description = "Image content block", Icon = "image")]
public partial class ImageBlock : IContentBlock
{
    public string BlockType => "Image";

    [BlockField("Src", Required = true, DisplayName = "Image Source")]
    public string Src { get; set; } = "";

    [BlockField("Alt", Required = true, DisplayName = "Alt Text", MaxLength = 250)]
    public string Alt { get; set; } = "";

    [BlockField("Caption")]
    public string? Caption { get; set; }

    [BlockField("Width")]
    public int? Width { get; set; }

    [BlockField("Height")]
    public int? Height { get; set; }
}
