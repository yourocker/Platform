using Core.Data;
using Core.Data.Interceptors;
using Core.Entities.Company;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Authorization;   
using Microsoft.AspNetCore.Mvc.Authorization;

#pragma warning disable CS0618
NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson(); 
#pragma warning restore CS0618

var builder = WebApplication.CreateBuilder(args);

// 1. Настройка контроллеров и политики авторизации
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

// 2. Получение строки подключения
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 3. Подключение БД с Interceptor (ИСПРАВЛЕНО)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Настройка PostgreSQL
    options.UseNpgsql(connectionString, npgsqlOptions => 
    {
        npgsqlOptions.EnableRetryOnFailure();
    });

    // Подключение перехватчика событий (Outbox)
    options.AddInterceptors(new OutboxInterceptor());
});

// 4. Настройка Identity
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

// 5. Настройка Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
});

var app = builder.Build();

// 6. Конвейер обработки запросов (Pipeline)
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

// 7. Автомиграция при старте
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
        // await DbInitializer.Initialize(context); 
        Console.WriteLine(">>> База данных готова и обновлена.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ошибка при инициализации БД");
    }
}

app.Run();