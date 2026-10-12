namespace FrenchExDev.Net.Diem.Pages.Layouts;

public class Layout
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string TemplateComponent { get; set; } = "";
    public ICollection<Area> Areas { get; set; } = new List<Area>();
}
