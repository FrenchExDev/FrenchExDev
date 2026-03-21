namespace FrenchExDev.Net.Diem.Admin.Lists;

public class AdminModuleDescriptor
{
    public required string Name { get; init; }
    public required Type AggregateType { get; init; }
    public string? Icon { get; init; }
    public string? Group { get; init; }
    public int PageSize { get; init; } = 25;
    public IReadOnlyList<AdminFilterDescriptor> Filters { get; init; } = [];
}

public class AdminFilterDescriptor
{
    public required string FieldName { get; init; }
    public string FilterType { get; init; } = "Text";
    public string? DisplayName { get; init; }
}
