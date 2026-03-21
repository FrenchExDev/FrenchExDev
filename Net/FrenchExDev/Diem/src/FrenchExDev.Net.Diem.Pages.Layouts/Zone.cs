namespace FrenchExDev.Net.Diem.Pages.Layouts;

public class Zone
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public string Name { get; set; } = "";
    public int MaxWidgets { get; set; } = 10;
    public Area? Area { get; set; }
}
