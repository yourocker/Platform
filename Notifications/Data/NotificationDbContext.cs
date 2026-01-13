using Microsoft.EntityFrameworkCore;
using System;

namespace Notifications.Data
{
    // Сущность для хранения истории уведомлений в базе notifications_db
    public class UserNotification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }

    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserNotification> UserNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Настройка таблицы истории
            modelBuilder.Entity<UserNotification>(entity =>
            {
                entity.ToTable("UserNotifications");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
            });
        }
    }
}