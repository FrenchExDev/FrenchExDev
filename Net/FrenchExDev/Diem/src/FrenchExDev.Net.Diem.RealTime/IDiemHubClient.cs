namespace FrenchExDev.Net.Diem.RealTime;

/// <summary>
/// SignalR hub for real-time admin collaboration.
/// </summary>
public interface IDiemHubClient
{
    Task ContentUpdated(string entityType, string entityId, string updatedBy);
    Task PageEdited(string pageId, string editedBy);
    Task WidgetMoved(string pageId, string zoneId, string widgetId);
    Task WorkflowTransitioned(string entityId, string fromStage, string toStage);
}
