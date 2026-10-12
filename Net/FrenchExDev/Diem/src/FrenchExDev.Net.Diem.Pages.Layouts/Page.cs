namespace FrenchExDev.Net.Diem.Pages.Layouts;

public class Page
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string MaterializedPath { get; set; } = "";
    public Guid? ParentId { get; set; }
    public Guid LayoutId { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public Page? Parent { get; set; }
    public ICollection<Page> Children { get; set; } = new List<Page>();
    public Layout? Layout { get; set; }
    public ICollection<WidgetInstance> WidgetInstances { get; set; } = new List<WidgetInstance>();
}
