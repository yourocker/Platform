using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Data;
using Core.Entities.System;
using CRM.Modules.Notifications.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace CRM.Modules.Notifications.Workers
{
    public class NotificationWorker : BackgroundService
    {
        private const string UserNotificationSourceEventIdIndexName = "IX_UserNotifications_SourceEventId";
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
            _logger.LogInformation("[NotificationWorker] Started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessEventsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[NotificationWorker] Failed to process notification outbox.");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        private async Task ProcessEventsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var eventIds = await context.OutboxEvents
                .Where(e => e.ProcessedAt == null)
                .OrderBy(e => e.CreatedAt)
                .Take(10)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken);

            if (!eventIds.Any())
            {
                return;
            }

            foreach (var eventId in eventIds)
            {
                try
                {
                    var notification = await PersistNotificationAsync(eventId, cancellationToken);

                    if (notification != null)
                    {
                        await SendRealtimeNotificationAsync(notification, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[NotificationWorker] Failed to process outbox event {OutboxEventId}.", eventId);
                }
            }
        }

        private async Task<PendingNotification?> PersistNotificationAsync(Guid eventId, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var evt = await context.OutboxEvents
                .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

            if (evt == null || evt.ProcessedAt != null)
            {
                return null;
            }

            if (await context.UserNotifications
                    .AsNoTracking()
                    .AnyAsync(n => n.SourceEventId == evt.Id, cancellationToken))
            {
                evt.ProcessedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                return null;
            }

            var data = JObject.Parse(evt.Payload);
            var recipientIdRaw = data["RecipientId"]?.ToString();

            if (!Guid.TryParse(recipientIdRaw, out var recipientId))
            {
                evt.ProcessedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                return null;
            }

            var userSettings = await context.Employees
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
                .FirstOrDefaultAsync(e => e.Id == recipientId, cancellationToken);

            if (userSettings == null)
            {
                _logger.LogWarning("[NotificationWorker] Recipient {RecipientId} not found.", recipientId);
                evt.ProcessedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                return null;
            }

            var canSendRealtime = !userSettings.IsAdvancedSettings
                ? userSettings.NotifyTaskGeneral
                : evt.EventType switch
                {
                    "TASK_ASSIGNED" => userSettings.NotifyTaskAssigned,
                    "TASK_COMMENT_ADDED" => userSettings.NotifyTaskComment,
                    _ => true
                };

            var createdAt = DateTime.UtcNow;
            var historyEntry = new UserNotification
            {
                Id = Guid.NewGuid(),
                SourceEventId = evt.Id,
                UserId = recipientId,
                Title = data["Title"]?.ToString() ?? "Уведомление",
                Message = data["Message"]?.ToString() ?? string.Empty,
                Url = data["Url"]?.ToString() ?? "#",
                CreatedAt = createdAt,
                IsRead = false
            };

            context.UserNotifications.Add(historyEntry);
            evt.ProcessedAt = createdAt;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsDuplicateSourceEventViolation(ex))
            {
                _logger.LogWarning(
                    "[NotificationWorker] Outbox event {OutboxEventId} was already persisted as a notification.",
                    evt.Id);
                return null;
            }

            if (!canSendRealtime)
            {
                return null;
            }

            return new PendingNotification
            {
                NotificationId = historyEntry.Id,
                RecipientId = recipientId,
                Title = historyEntry.Title,
                Message = historyEntry.Message,
                Url = historyEntry.Url,
                CreatedAt = historyEntry.CreatedAt,
                PlaySound = userSettings.NotifySoundEnabled,
                ShowDesktop = userSettings.NotifyDesktopEnabled
            };
        }

        private async Task SendRealtimeNotificationAsync(
            PendingNotification notification,
            CancellationToken cancellationToken)
        {
            try
            {
                await _hubContext.Clients.User(notification.RecipientId.ToString()).SendAsync(
                    "ReceiveNotification",
                    new
                    {
                        id = notification.NotificationId,
                        title = notification.Title,
                        message = notification.Message,
                        url = notification.Url,
                        createdAt = notification.CreatedAt,
                        playSound = notification.PlaySound,
                        showDesktop = notification.ShowDesktop
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[NotificationWorker] Failed to push realtime notification {NotificationId} to user {RecipientId}.",
                    notification.NotificationId,
                    notification.RecipientId);
            }
        }

        private static bool IsDuplicateSourceEventViolation(DbUpdateException exception)
        {
            return exception.InnerException is PostgresException postgresException
                   && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                   && string.Equals(
                       postgresException.ConstraintName,
                       UserNotificationSourceEventIdIndexName,
                       StringComparison.Ordinal);
        }

        private sealed class PendingNotification
        {
            public Guid NotificationId { get; init; }
            public Guid RecipientId { get; init; }
            public string Title { get; init; } = string.Empty;
            public string Message { get; init; } = string.Empty;
            public string Url { get; init; } = string.Empty;
            public DateTime CreatedAt { get; init; }
            public bool PlaySound { get; init; }
            public bool ShowDesktop { get; init; }
        }
    }
}
