namespace FrenchExDev.Net.Diem.Content.Parts;

using FrenchExDev.Net.Diem.Content.Parts.Attributes;

[ContentPart("Mediable", Description = "Media attachment support")]
public partial class MediablePart
{
    [PartField("MediaPath", DisplayName = "Media Path", HelpText = "Path or URI to the media asset")]
    public string MediaPath { get; set; } = "";

    [PartField("AltText", DisplayName = "Alt Text", MaxLength = 250)]
    public string? AltText { get; set; }

    [PartField("MimeType", DisplayName = "MIME Type")]
    public string? MimeType { get; set; }

    [PartField("FileSizeBytes", DisplayName = "File Size (bytes)")]
    public long FileSizeBytes { get; set; }
}
