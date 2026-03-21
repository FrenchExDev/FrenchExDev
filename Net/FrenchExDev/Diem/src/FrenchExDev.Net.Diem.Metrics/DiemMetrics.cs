namespace FrenchExDev.Net.Diem.Metrics;

using System.Diagnostics.Metrics;

public sealed class DiemMetrics
{
    public static readonly Meter Meter = new("FrenchExDev.Net.Diem", "1.0.0");

    public static readonly Counter<long> PageViews = Meter.CreateCounter<long>("diem.page.views", description: "Page views");
    public static readonly Histogram<double> WidgetRenderDuration = Meter.CreateHistogram<double>("diem.widget.render_duration", "ms", "Widget render time");
    public static readonly Counter<long> ContentEdits = Meter.CreateCounter<long>("diem.content.edits", description: "Content edits");
    public static readonly Counter<long> WorkflowTransitions = Meter.CreateCounter<long>("diem.workflow.transitions", description: "Workflow transitions");
    public static readonly Counter<long> SearchQueries = Meter.CreateCounter<long>("diem.search.queries", description: "Search queries");
    public static readonly Counter<long> MediaUploads = Meter.CreateCounter<long>("diem.media.uploads", description: "Media uploads");
}
