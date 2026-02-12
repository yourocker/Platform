using Core.Data;
using Core.Entities.Company;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Authorization;   
using Microsoft.AspNetCore.Mvc.Authorization;
using Core.Interfaces.CRM;
using Core.Services.CRM;
using Core.Interfaces;
using Core.Services;

#pragma warning disable CS0618
// Поддержка работы с jsonb в PostgreSQL
NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson(); 
#pragma warning restore CS0618

var builder = WebApplication.CreateBuilder(args);

// 1. Настройка контроллеров и глобальная политика авторизации
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

// 1.1. Сервисы платформы
builder.Services.AddScoped<ITransliterationService, TransliterationService>();

// 2. Настройка базы данных
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions => 
    {
        npgsqlOptions.EnableRetryOnFailure();
    });
    // Интерцептор Outbox теперь подключен внутри самого AppDbContext.cs (OnConfiguring)
});

// 3. Настройка Identity (Аутентификация и пользователи)
builder.Services.AddIdentity<Employee, IdentityRole<Guid>>(options => 
    {
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 4; 
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// 4. Настройка Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IContactService, ContactService>();

builder.Services.AddScoped<Core.Services.ICrmStyleService, Core.Services.CrmStyleService>();

var app = builder.Build();

// 5. Конвейер обработки (Middleware)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 6. Автоматическое применение миграций и ИНИЦИАЛИЗАЦИЯ ДАННЫХ ПРИ СТАРТЕ
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<Employee>>();
        var configuration = services.GetRequiredService<IConfiguration>(); // Получаем конфиг
        
        // Сначала накатываем миграции
        await context.Database.MigrateAsync();
        
        // ЗАТЕМ запускаем твой инициализатор для создания CRM, Контактов и Пользователя
        await DbInitializer.Initialize(context, userManager, configuration);
        
        Console.WriteLine(">>> CRM: База данных готова и метаданные инициализированы.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ошибка при инициализации БД CRM");
    }
}

app.Run();