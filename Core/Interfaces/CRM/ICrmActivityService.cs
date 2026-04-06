using Core.Entities.CRM;

namespace Core.Interfaces.CRM;

public interface ICrmActivityService
{
    Task<IReadOnlyList<CrmActivity>> GetActivitiesAsync(Guid entityId, string entityCode);

    Task<CrmActivity> AddCommentAsync(
        Guid entityId,
        string entityCode,
        string text,
        Guid authorId);

    Task<CrmActivity> CreateTaskAsync(
        Guid entityId,
        string entityCode,
        string title,
        string? description,
        Guid authorId,
        Guid assigneeId,
        DateTime? deadline);
}
