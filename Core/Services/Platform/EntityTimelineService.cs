using Core.Data;
using Core.Entities.CRM;
using Core.Interfaces.Platform;
using Microsoft.EntityFrameworkCore;

namespace Core.Services.Platform;

public class EntityTimelineService(AppDbContext context) : IEntityTimelineService
{
    public async Task LogEventAsync(
        Guid targetId,
        string entityCode,
        CrmEventType type,
        string title,
        string? content = null,
        Guid? employeeId = null,
        bool isPinned = false)
    {
        var entityEvent = new CrmEvent
        {
            Id = Guid.NewGuid(),
            TargetId = targetId,
            TargetEntityCode = entityCode,
            Type = type,
            Title = title,
            Content = content,
            EmployeeId = employeeId,
            CreatedAt = DateTime.UtcNow,
            IsPinned = isPinned
        };

        context.CrmEvents.Add(entityEvent);
        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<CrmEvent>> GetEventsAsync(
        Guid targetId,
        string entityCode,
        bool includeViews = false)
    {
        var query = context.CrmEvents
            .AsNoTracking()
            .Include(e => e.Employee)
            .Where(e => e.TargetId == targetId && e.TargetEntityCode == entityCode);

        if (!includeViews)
        {
            query = query.Where(e => e.Type != CrmEventType.View);
        }

        return await query
            .OrderByDescending(e => e.IsPinned)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync();
    }
}
