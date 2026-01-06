using MedicalBot.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Оставляем для поддержки динамического маппинга JSONB в Postgres
#pragma warning disable CS0618
NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson(); 
#pragma warning restore CS0618

var builder = WebApplication.CreateBuilder(args);

// Добавляем сервисы MVC
builder.Services.AddControllersWithViews();

// Берем строку подключения один раз
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, o => 
    {
        // Включаем отказоустойчивость
        o.EnableRetryOnFailure();
    }));

var app = builder.Build();

// Настройка обработки ошибок
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// --- БЛОК ИНИЦИАЛИЗАЦИИ ПЛАТФОРМЫ ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        
        // Автоматически применяем миграции
        await context.Database.MigrateAsync();
        
        // Наполняем базовыми определениями системных сущностей (Employee, Patient и т.д.)
        await DbInitializer.Initialize(context);
        
        Console.WriteLine(">>> База данных и платформенные сущности успешно инициализированы.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ошибка при инициализации БД");
    }
}

// Запускаем приложение (ТОЛЬКО ОДИН РАЗ)
app.Run();