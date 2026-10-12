namespace FrenchExDev.Net.Diem.Pages.Layouts;

public class Area
{
    public Guid Id { get; set; }
    public Guid LayoutId { get; set; }
    public string Name { get; set; } = "";
    public Layout? Layout { get; set; }
    public ICollection<Zone> Zones { get; set; } = new List<Zone>();
}
