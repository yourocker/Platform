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

            // Получаем все новые сущности до момента сохранения
            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added)
                .ToList();

            foreach (var entry in entries)
            {
                // Обработка создания задачи
                if (entry.Entity is EmployeeTask task)
                {
                    if (task.AssigneeId != Guid.Empty)
                    {
                        Console.WriteLine($">>> [OutboxInterceptor] Фиксация задачи: {task.Title} для {task.AssigneeId}");

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

                // Обработка комментария
                if (entry.Entity is TaskComment comment)
                {
                    Console.WriteLine($">>> [OutboxInterceptor] Фиксация комментария к задаче: {comment.TaskId}");

                    var payload = new
                    {
                        TaskId = comment.TaskId,
                        SenderId = comment.AuthorId,
                        Title = "Новый комментарий",
                        Message = "В вашей задаче появился новый комментарий",
                        Url = $"/Tasks/Details/{comment.TaskId}"
                    };

                    CreateEvent(context, "TASK_COMMENT_ADDED", payload);
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void CreateEvent(DbContext context, string type, object data)
        {
            var outboxEvent = new OutboxEvent
            {
                Id = Guid.NewGuid(),
                EventType = type,
                Payload = JsonConvert.SerializeObject(data),
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = null // Обязательно null для воркера
            };

            context.Set<OutboxEvent>().Add(outboxEvent);
            Console.WriteLine($">>> [OutboxInterceptor] ✅ Событие {type} поставлено в очередь сохранения.");
        }
    }
}