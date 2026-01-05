using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace MedicalBot.Bot;

public class BotBackgroundService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider; // Используем провайдер для создания Scope

    public BotBackgroundService(ITelegramBotClient botClient, IServiceProvider serviceProvider)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // Слушаем все типы обновлений
        };

        // Запускаем получение обновлений с использованием лямбда-выражений для создания Scope
        _botClient.StartReceiving(
            updateHandler: async (bot, update, ct) => 
            {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();
                await handler.HandleUpdateAsync(bot, update, ct);
            },
            pollingErrorHandler: async (bot, ex, ct) => 
            {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();
                await handler.HandlePollingErrorAsync(bot, ex, ct);
            },
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        Console.WriteLine("Бот запущен и ожидает сообщений (Scoped Mode)...");
        
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}