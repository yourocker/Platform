using Core.Bot;
using Core.Configuration;
using Core.Data;
using Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

// 1. Builder create
var builder = Host.CreateApplicationBuilder(args);

// 2. Add configs appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Settings reg as Singleton
var appConfig = builder.Configuration.Get<AppConfig>() 
                ?? throw new Exception("Не удалось загрузить конфигурацию!");
builder.Services.AddSingleton(appConfig);

// 3. Connect DB DI
var connectionString = appConfig.ConnectionStrings["DefaultConnection"];
builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseNpgsql(connectionString));

// 4. Register Telegram Bot Client
builder.Services.AddSingleton<ITelegramBotClient>(provider => 
    new TelegramBotClient(appConfig.BotToken));

// 5. Service reg
builder.Services.AddTransient<PatientService>();
builder.Services.AddTransient<StatisticsService>();
builder.Services.AddTransient<PatientImporter>();
builder.Services.AddTransient<ExcelImporter>(provider => new ExcelImporter(connectionString));
builder.Services.AddTransient<AppointmentImporter>(provider => new AppointmentImporter(connectionString));

// Регистрируем обработчик сообщений
builder.Services.AddScoped<UpdateHandler>();
//builder.Services.AddSingleton<UpdateHandler>();

// 6. Регистрируем фоновый сервис, который "держит" бота включенным
builder.Services.AddHostedService<BotBackgroundService>();

// 7. Собираем и запускаем приложение
var host = builder.Build();

Console.WriteLine("Приложение инициализировано. Запуск сервисов...");

await host.RunAsync();