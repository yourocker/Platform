using Core.Data;
using Core.Entities.System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Notifications.Data;
using Notifications.Hubs;

namespace Notifications.Workers
{
    public class NotificationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<NotificationWorker> _logger;

        public NotificationWorker(
            IServiceProvider serviceProvider, 
            IHubContext<NotificationHub> hubContext,
            ILogger<NotificationWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(">>> [Worker] Запущен. Мониторинг CRM DB и запись в Notifications DB...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessEventsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ">>> [Worker] Ошибка при обработке событий");
                }

                await Task.Delay(2000, stoppingToken);
            }
        }

        private async Task ProcessEventsAsync(CancellationToken token)
        {
            using var scope = _serviceProvider.CreateScope();
            
            // Контекст базы CRM (medical_db) - только для очереди событий
            var crmContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            // Контекст собственной базы (notifications_db) - для истории
            var notifyContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            var events = await crmContext.OutboxEvents
                .Where(e => e.ProcessedAt == null)
                .OrderBy(e => e.CreatedAt)
                .Take(10)
                .ToListAsync(token);

            if (!events.Any()) return;

            _logger.LogInformation($">>> [Worker] Найдено событий: {events.Count}");

            foreach (var evt in events)
            {
                try
                {
                    var data = JObject.Parse(evt.Payload);
                    var recipientIdStr = data["RecipientId"]?.ToString();

                    if (Guid.TryParse(recipientIdStr, out var recipientId))
                    {
                        var title = data["Title"]?.ToString() ?? "Уведомление";
                        var message = data["Message"]?.ToString() ?? "";
                        var url = data["Url"]?.ToString() ?? "#";

                        // 1. Сохраняем в историю собственной базы данных (notifications_db)
                        var historyEntry = new UserNotification
                        {
                            Id = Guid.NewGuid(),
                            UserId = recipientId,
                            Title = title,
                            Message = message,
                            Url = url,
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false
                        };

                        notifyContext.UserNotifications.Add(historyEntry);

                        // 2. Отправляем сигнал в браузер
                        await _hubContext.Clients.User(recipientId.ToString()).SendAsync("ReceiveNotification", new 
                        { 
                            id = historyEntry.Id,
                            title = title,
                            message = message,
                            url = url
                        }, token);

                        _logger.LogInformation($">>> [Worker] Обработано: {evt.EventType} для {recipientId}");
                    }
                    
                    // Помечаем событие в базе CRM как обработанное
                    evt.ProcessedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $">>> [Worker] Ошибка обработки события {evt.Id}");
                }
            }

            // Сохраняем изменения в обеих базах
            await notifyContext.SaveChangesAsync(token);
            await crmContext.SaveChangesAsync(token);
            
            _logger.LogInformation(">>> [Worker] Данные синхронизированы в обеих БД.");
        }
    }
}