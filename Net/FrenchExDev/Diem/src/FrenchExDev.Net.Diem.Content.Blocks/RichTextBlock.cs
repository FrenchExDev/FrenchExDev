namespace FrenchExDev.Net.Diem.Content.Blocks;

using FrenchExDev.Net.Diem.Content.Blocks.Attributes;

[StructBlock("RichText", Description = "Rich text content block", Icon = "text")]
public partial class RichTextBlock : IContentBlock
{
    public string BlockType => "RichText";

    [BlockField("Body", Required = true, HelpText = "HTML or Markdown content")]
    public string Body { get; set; } = "";

    [BlockField("Format", DisplayName = "Content Format", HelpText = "html or markdown")]
    public string Format { get; set; } = "html";
}
