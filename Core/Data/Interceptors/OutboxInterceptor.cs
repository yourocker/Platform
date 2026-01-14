using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Entities.System;
using Core.Entities.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Newtonsoft.Json;

namespace Core.Data.Interceptors
{
    public class OutboxInterceptor : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, 
            InterceptionResult<int> result, 
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context == null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added)
                .ToList();

            foreach (var entry in entries)
            {
                // 1. Обработка новой задачи
                if (entry.Entity is EmployeeTask task)
                {
                    if (task.AssigneeId != Guid.Empty)
                    {
                        var payload = new
                        {
                            TaskId = task.Id,
                            RecipientId = task.AssigneeId,
                            SenderId = task.AuthorId,
                            Title = "Новая задача",
                            Message = $"Вам назначена задача: {task.Title}",
                            Url = $"/Tasks/Details/{task.Id}"
                        };
                        CreateEvent(context, "TASK_ASSIGNED", payload);
                    }
                }

                // 2. Обработка нового комментария
                if (entry.Entity is TaskComment comment)
                {
                    // Ищем задачу, чтобы понять, кому слать уведомление
                    var taskInfo = await context.Set<EmployeeTask>()
                        .AsNoTracking()
                        .Select(t => new { t.Id, t.AuthorId, t.AssigneeId })
                        .FirstOrDefaultAsync(t => t.Id == comment.TaskId, cancellationToken);

                    if (taskInfo != null)
                    {
                        // Если пишет автор задачи - уведомляем исполнителя, и наоборот
                        var recipientId = comment.AuthorId == taskInfo.AuthorId 
                            ? taskInfo.AssigneeId 
                            : taskInfo.AuthorId;

                        if (recipientId != Guid.Empty && recipientId != comment.AuthorId)
                        {
                            var payload = new
                            {
                                TaskId = comment.TaskId,
                                RecipientId = recipientId, // ТЕПЕРЬ ВОРКЕР УВИДИТ ПОЛУЧАТЕЛЯ
                                SenderId = comment.AuthorId,
                                Title = "Новый комментарий",
                                Message = "В вашей задаче появился новый комментарий",
                                Url = $"/Tasks/Details/{comment.TaskId}"
                            };
                            CreateEvent(context, "TASK_COMMENT_ADDED", payload);
                        }
                    }
                }
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void CreateEvent(DbContext context, string type, object data)
        {
            var outboxEvent = new OutboxEvent
            {
                Id = Guid.NewGuid(),
                EventType = type,
                Payload = JsonConvert.SerializeObject(data),
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = null
            };
            context.Set<OutboxEvent>().Add(outboxEvent);
            Console.WriteLine($">>> [OutboxInterceptor] Событие {type} добавлено в очередь.");
        }
    }
}