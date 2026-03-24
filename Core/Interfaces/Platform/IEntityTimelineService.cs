using Core.Entities.CRM;

namespace Core.Interfaces.Platform;

public interface IEntityTimelineService
{
    Task LogEventAsync(
        Guid targetId,
        string entityCode,
        CrmEventType type,
        string title,
        string? content = null,
        Guid? employeeId = null,
        bool isPinned = false);

    Task<IReadOnlyList<CrmEvent>> GetEventsAsync(
        Guid targetId,
        string entityCode,
        bool includeViews = false);
}
