using Core.Data;
using Core.Entities.Company;
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
            _logger.LogInformation(">>> [Worker] Запущен. Мониторинг CRM DB (с учетом настроек пользователя)...");

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
            var crmContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notifyContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            var events = await crmContext.OutboxEvents
                .Where(e => e.ProcessedAt == null)
                .OrderBy(e => e.CreatedAt)
                .Take(10)
                .ToListAsync(token);

            if (!events.Any()) return;

            foreach (var evt in events)
            {
                try
                {
                    var data = JObject.Parse(evt.Payload);
                    var recipientIdStr = data["RecipientId"]?.ToString();

                    if (Guid.TryParse(recipientIdStr, out var recipientId))
                    {
                        // 1. Получаем настройки пользователя из базы CRM
                        var userSettings = await crmContext.Employees
                            .AsNoTracking()
                            .Select(e => new 
                            { 
                                e.Id, 
                                e.NotifySoundEnabled, 
                                e.NotifyDesktopEnabled,
                                e.IsAdvancedSettings,
                                e.NotifyTaskGeneral,
                                e.NotifyTaskAssigned,
                                e.NotifyTaskComment
                            })
                            .FirstOrDefaultAsync(e => e.Id == recipientId, token);

                        if (userSettings == null)
                        {
                            _logger.LogWarning($">>> [Worker] Пользователь {recipientId} не найден.");
                            evt.ProcessedAt = DateTime.UtcNow;
                            continue;
                        }

                        // 2. Логика фильтрации: нужно ли отправлять сигнал?
                        bool canSendRealtime = false;

                        if (!userSettings.IsAdvancedSettings)
                        {
                            // ОБЫЧНЫЙ режим: смотрим только общую галку задач
                            canSendRealtime = userSettings.NotifyTaskGeneral;
                        }
                        else
                        {
                            // РАСШИРЕННЫЙ режим: проверяем конкретный тип
                            canSendRealtime = evt.EventType switch
                            {
                                "TASK_ASSIGNED" => userSettings.NotifyTaskAssigned,
                                "TASK_COMMENT_ADDED" => userSettings.NotifyTaskComment,
                                _ => true
                            };
                        }

                        // 3. Сохраняем в историю ВСЕГДА (чтобы в списке боковой панели уведомление было)
                        var historyEntry = new UserNotification
                        {
                            Id = Guid.NewGuid(),
                            UserId = recipientId,
                            Title = data["Title"]?.ToString() ?? "Уведомление",
                            Message = data["Message"]?.ToString() ?? "",
                            Url = data["Url"]?.ToString() ?? "#",
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false
                        };
                        notifyContext.UserNotifications.Add(historyEntry);

                        // 4. Отправляем SignalR только если разрешено настройками
                        if (canSendRealtime)
                        {
                            await _hubContext.Clients.User(recipientId.ToString()).SendAsync("ReceiveNotification", new 
                            { 
                                id = historyEntry.Id,
                                title = historyEntry.Title,
                                message = historyEntry.Message,
                                url = historyEntry.Url,
                                // Передаем флаги, чтобы JS знал, играть ли звук и показывать ли пуш
                                playSound = userSettings.NotifySoundEnabled,
                                showDesktop = userSettings.NotifyDesktopEnabled
                            }, token);
                        }
                    }
                    
                    evt.ProcessedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $">>> [Worker] Ошибка на событии {evt.Id}");
                }
            }

            await notifyContext.SaveChangesAsync(token);
            await crmContext.SaveChangesAsync(token);
        }
    }
}