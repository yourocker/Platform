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
using Core.Interfaces.Platform;
using Core.Services.Platform;
using CRM.Infrastructure;
using CRM.Modules.Notifications.Hubs;
using CRM.Modules.Notifications.Infrastructure;
using CRM.Modules.Notifications.Workers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

#pragma warning disable CS0618
// Поддержка работы с jsonb в PostgreSQL
NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson(); 
#pragma warning restore CS0618

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// 1. Настройка контроллеров и глобальная политика авторизации
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
    options.Filters.AddService<FeatureGateFilter>();
    options.Filters.AddService<ModalRedirectFilter>();
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
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequiredUniqueChars = 4;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// 4. Настройка Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();

builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<ICrmService, CrmService>();
builder.Services.AddScoped<ICrmResourceManager, CrmResourceManager>();
builder.Services.AddScoped<IBookingPolicyService, BookingPolicyService>();
builder.Services.AddScoped<IBookingCalendarDecorationService, BookingCalendarDecorationService>();
builder.Services.AddScoped<IFeatureToggleService, FeatureToggleService>();
builder.Services.AddScoped<IEntityTimelineService, EntityTimelineService>();
builder.Services.AddScoped<FeatureGateFilter>();
builder.Services.AddScoped<ModalRedirectFilter>();

builder.Services.AddScoped<Core.Services.ICrmStyleService, Core.Services.CrmStyleService>();
builder.Services.AddHostedService<NotificationWorker>();

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

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications").RequireAuthorization();
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
