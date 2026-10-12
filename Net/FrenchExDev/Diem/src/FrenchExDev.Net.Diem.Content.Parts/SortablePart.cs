namespace FrenchExDev.Net.Diem.Content.Parts;

using FrenchExDev.Net.Diem.Content.Parts.Attributes;

[ContentPart("Sortable", Description = "Manual ordering support")]
public partial class SortablePart
{
    [PartField("SortOrder", DisplayName = "Sort Order")]
    public int SortOrder { get; set; }

    [PartField("SortGroup", DisplayName = "Sort Group", HelpText = "Optional grouping key for scoped ordering")]
    public string? SortGroup { get; set; }
}
