using Core.Data;
using Core.Entities.CRM;
using Core.Entities.Tasks;
using Core.Entities.System;
using Core.Interfaces.CRM;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Core.Services.CRM;

public class CrmActivityService(AppDbContext context) : ICrmActivityService
{
    public async Task<IReadOnlyList<CrmActivity>> GetActivitiesAsync(Guid entityId, string entityCode)
    {
        return await context.CrmActivities
            .AsNoTracking()
            .Include(x => x.Author)
            .Include(x => x.LinkedTask)
                .ThenInclude(x => x!.Author)
            .Include(x => x.LinkedTask)
                .ThenInclude(x => x!.Assignee)
            .Where(x => x.Bindings.Any(b => b.EntityId == entityId && b.EntityCode == entityCode))
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<CrmActivity> AddCommentAsync(
        Guid entityId,
        string entityCode,
        string text,
        Guid authorId)
    {
        var normalizedText = text.Trim();
        var activity = new CrmActivity
        {
            Id = Guid.NewGuid(),
            Type = CrmActivityType.Comment,
            Subject = "Комментарий",
            Content = normalizedText,
            AuthorId = authorId,
            CreatedAt = DateTime.UtcNow,
            Bindings =
            {
                new CrmActivityBinding
                {
                    EntityCode = entityCode,
                    EntityId = entityId,
                    IsPrimary = true
                }
            }
        };

        context.CrmActivities.Add(activity);
        await context.SaveChangesAsync();

        var recipientId = await ResolveResponsibleIdAsync(entityId, entityCode);
        await CreateEntityNotificationAsync(
            $"Новый комментарий по {ResolveEntityLabel(entityCode)}",
            normalizedText,
            $"/{entityCode}s/Details/{entityId}",
            recipientId,
            authorId);

        return activity;
    }

    public async Task<CrmActivity> CreateTaskAsync(
        Guid entityId,
        string entityCode,
        string title,
        string? description,
        Guid authorId,
        Guid assigneeId,
        DateTime? deadline)
    {
        var normalizedTitle = title.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
        var taskId = Guid.NewGuid();
        var normalizedDeadline = NormalizeDateTime(deadline);

        var task = new EmployeeTask
        {
            Id = taskId,
            Name = normalizedTitle,
            Title = normalizedTitle,
            Description = normalizedDescription,
            EntityCode = "EmployeeTask",
            AuthorId = authorId,
            AssigneeId = assigneeId,
            Status = Core.Entities.Tasks.TaskStatus.Created,
            CreatedAt = DateTime.UtcNow,
            Deadline = normalizedDeadline,
            Relations =
            {
                new TaskEntityRelation
                {
                    TaskId = taskId,
                    EntityCode = entityCode,
                    EntityId = entityId,
                    EntityName = await ResolveEntityNameAsync(entityId, entityCode)
                }
            }
        };

        var activity = new CrmActivity
        {
            Id = Guid.NewGuid(),
            Type = CrmActivityType.Task,
            Subject = normalizedTitle,
            Content = normalizedDescription,
            AuthorId = authorId,
            LinkedTaskId = taskId,
            DueAt = normalizedDeadline,
            CreatedAt = DateTime.UtcNow,
            Bindings =
            {
                new CrmActivityBinding
                {
                    EntityCode = entityCode,
                    EntityId = entityId,
                    IsPrimary = true
                }
            }
        };

        context.EmployeeTasks.Add(task);
        context.CrmActivities.Add(activity);
        await context.SaveChangesAsync();

        await CreateEntityNotificationAsync(
            $"Новая задача по {ResolveEntityLabel(entityCode)}",
            normalizedTitle,
            $"/Tasks/Details/{taskId}",
            assigneeId,
            authorId);

        return activity;
    }

    private async Task<string?> ResolveEntityNameAsync(Guid entityId, string entityCode)
    {
        return entityCode switch
        {
            "Lead" => await context.Leads
                .AsNoTracking()
                .Where(x => x.Id == entityId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(),
            "Deal" => await context.Deals
                .AsNoTracking()
                .Where(x => x.Id == entityId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(),
            _ => null
        };
    }

    private async Task<Guid?> ResolveResponsibleIdAsync(Guid entityId, string entityCode)
    {
        return entityCode switch
        {
            "Lead" => await context.Leads
                .AsNoTracking()
                .Where(x => x.Id == entityId)
                .Select(x => x.ResponsibleId)
                .FirstOrDefaultAsync(),
            "Deal" => await context.Deals
                .AsNoTracking()
                .Where(x => x.Id == entityId)
                .Select(x => x.ResponsibleId)
                .FirstOrDefaultAsync(),
            _ => null
        };
    }

    private async Task CreateEntityNotificationAsync(
        string title,
        string message,
        string url,
        Guid? recipientId,
        Guid actorId)
    {
        if (!recipientId.HasValue || recipientId.Value == Guid.Empty || recipientId.Value == actorId)
        {
            return;
        }

        var payload = new
        {
            RecipientId = recipientId.Value,
            Title = $"CRM: {title}",
            Message = message,
            Url = url
        };

        context.OutboxEvents.Add(new OutboxEvent
        {
            Id = Guid.NewGuid(),
            EventType = "CRM_NOTIFICATION",
            Payload = JsonConvert.SerializeObject(payload),
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static string ResolveEntityLabel(string entityCode)
    {
        return entityCode switch
        {
            "Lead" => "лиду",
            "Deal" => "сделке",
            _ => "CRM-элементу"
        };
    }

    private static DateTime? NormalizeDateTime(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Local).ToUniversalTime()
        };
    }
}
