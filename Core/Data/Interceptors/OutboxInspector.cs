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
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, 
            InterceptionResult<int> result, 
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context == null) return base.SavingChangesAsync(eventData, result, cancellationToken);

            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .ToList();

            foreach (var entry in entries)
            {
                // 1. Новая задача (EmployeeTask)
                if (entry.Entity is EmployeeTask task && entry.State == EntityState.Added)
                {
                    AddOutboxEvent(context, "TASK_ASSIGNED", new
                    {
                        TaskId = task.Id,
                        RecipientId = task.AssigneeId, // ID исполнителя
                        SenderId = task.AuthorId,      // ID автора (постановщика)
                        Title = "Новая задача",
                        Message = $"Вам назначена задача: {task.Title}",
                        Url = $"/Tasks/Details/{task.Id}"
                    });
                }

                // 2. Новый комментарий (TaskComment)
                if (entry.Entity is TaskComment comment && entry.State == EntityState.Added)
                {
                    // Для комментария мы передаем SenderId и TaskId.
                    // Сервис уведомлений сам разберется, кому отправлять (автору задачи или исполнителю),
                    // подгрузив задачу по TaskId.
                    AddOutboxEvent(context, "TASK_COMMENT_ADDED", new
                    {
                        TaskId = comment.TaskId,       // ID задачи
                        SenderId = comment.AuthorId,   // Кто написал
                        Title = "Новый комментарий",
                        Message = "В задаче появился новый комментарий", 
                        Preview = comment.Text.Length > 50 ? comment.Text.Substring(0, 50) + "..." : comment.Text,
                        Url = $"/Tasks/Details/{comment.TaskId}"
                    });
                }
                
                // Здесь можно добавлять обработку TASK_STATUS_CHANGED и других событий
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void AddOutboxEvent(DbContext context, string type, object data)
        {
            context.Set<OutboxEvent>().Add(new OutboxEvent
            {
                Id = Guid.NewGuid(),
                EventType = type,
                Payload = JsonConvert.SerializeObject(data),
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}