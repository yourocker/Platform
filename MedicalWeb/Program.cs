using MedicalBot.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

#pragma warning disable CS0618
NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson(); 
#pragma warning restore CS0618

var builder = WebApplication.CreateBuilder(args);

// Добавляем сервисы MVC
builder.Services.AddControllersWithViews();

// Подключаем базу данных (Исправил название на AppDbContext)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => 
    {
        // Вот эта магическая настройка разрешает маппинг коллекций в JSON
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

app.Run();