namespace FrenchExDev.Net.Diem.Content.Parts;

using FrenchExDev.Net.Diem.Content.Parts.Attributes;

[ContentPart("Localizable", Description = "Localization and culture support")]
public partial class LocalizablePart
{
    [PartField("Culture", Required = true, HelpText = "IETF language tag (e.g. en-US)")]
    public string Culture { get; set; } = "";

    [PartField("LocalizationSet", DisplayName = "Localization Set", HelpText = "Groups translated variants")]
    public string LocalizationSet { get; set; } = "";

    [PartField("IsDefault", DisplayName = "Default Locale")]
    public bool IsDefault { get; set; }
}
