using Core.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Notifications.Data; // Наш новый контекст
using Notifications.Hubs;
using Notifications.Infrastructure;
using Notifications.Workers;

var builder = WebApplication.CreateBuilder(args);

// 1. Настройка подключения к базе CRM (Только для чтения OutboxEvents)
var crmConnectionString = builder.Configuration.GetConnectionString("CrmDbConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(crmConnectionString));

// 2. Настройка подключения к собственной базе Notifications (Для истории уведомлений)
var notificationsConnectionString = builder.Configuration.GetConnectionString("NotificationsDbConnection");
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(notificationsConnectionString));

// 3. Настройка CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "https://localhost:7262",
                "http://localhost:5140"
            ) 
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); 
    });
});

// 4. SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, QueryStringUserIdProvider>();

// 5. Воркер (теперь будет использовать оба контекста)
builder.Services.AddHostedService<NotificationWorker>();

builder.Services.AddControllers();

var app = builder.Build();

// Автоматическое создание базы данных уведомлений и применение миграций
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

Console.WriteLine(">>> Сервис уведомлений (Notifications) запущен с разделением БД!");

app.Run();