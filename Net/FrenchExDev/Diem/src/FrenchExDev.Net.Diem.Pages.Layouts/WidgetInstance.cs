namespace FrenchExDev.Net.Diem.Pages.Layouts;

public class WidgetInstance
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public Guid ZoneId { get; set; }
    public string WidgetType { get; set; } = "";
    public int SortOrder { get; set; }
    public string ConfigurationJson { get; set; } = "{}";

    public Page? Page { get; set; }
    public Zone? Zone { get; set; }
}
