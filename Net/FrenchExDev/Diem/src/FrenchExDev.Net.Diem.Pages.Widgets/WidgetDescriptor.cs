namespace FrenchExDev.Net.Diem.Pages.Widgets;

public sealed class WidgetDescriptor
{
    public required string Name { get; init; }
    public required Type ComponentType { get; init; }
    public required string Module { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<WidgetConfigDescriptor> ConfigProperties { get; init; } = [];
}

public sealed class WidgetConfigDescriptor
{
    public required string PropertyName { get; init; }
    public required Type PropertyType { get; init; }
    public bool Required { get; init; }
    public string? DefaultValue { get; init; }
    public string? DisplayName { get; init; }
}
