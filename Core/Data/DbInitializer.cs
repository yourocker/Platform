using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Entities.Platform;
using Microsoft.EntityFrameworkCore;

namespace Core.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(AppDbContext context)
        {
            // --- ШАГ 1: Инициализация разделов меню (AppCategories) ---
            // Оставляем только те разделы, которые реально должны быть в левом меню
            var categories = new List<AppCategory>
            {
                new AppCategory { Id = Guid.Parse("e5555555-5555-5555-5555-555555555555"), Name = "CRM", Icon = "wallet-fill", SortOrder = 2, IsSystem = true },
                new AppCategory { Id = Guid.Parse("a1111111-1111-1111-1111-111111111111"), Name = "Компания", Icon = "building", SortOrder = 1, IsSystem = true },
                new AppCategory { Id = Guid.Parse("c3333333-3333-3333-3333-333333333333"), Name = "Задачи", Icon = "list-check", SortOrder = 3, IsSystem = true }
            };

            foreach (var cat in categories)
            {
                var existingCat = await context.AppCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cat.Id);
                if (existingCat == null)
                {
                    context.AppCategories.Add(cat);
                }
                else
                {
                    context.Entry(cat).State = EntityState.Modified;
                }
            }
            await context.SaveChangesAsync();

            // Получаем ссылки на созданные категории для привязки приложений
            var catCRM = await context.AppCategories.FirstOrDefaultAsync(c => c.Name == "CRM");
            var catCompany = await context.AppCategories.FirstOrDefaultAsync(c => c.Name == "Компания");
            var catTasks = await context.AppCategories.FirstOrDefaultAsync(c => c.Name == "Задачи");

            // --- ШАГ 2: Регистрация системных приложений (AppDefinitions) ---
            var systemApps = new List<AppDefinition>
            {
                // CRM
                new AppDefinition { Name = "Контакты", EntityCode = "Contact", Icon = "person-lines-fill", IsSystem = true, AppCategoryId = catCRM?.Id },
                
                // Компания (Сюда теперь входят и Услуги, и Задачи)
                new AppDefinition { Name = "Сотрудники", EntityCode = "Employee", Icon = "person-badge", IsSystem = true, AppCategoryId = catCompany?.Id },
                new AppDefinition { Name = "Должности", EntityCode = "Position", Icon = "briefcase", IsSystem = true, AppCategoryId = catCompany?.Id },
                new AppDefinition { Name = "Подразделения", EntityCode = "Department", Icon = "diagram-3", IsSystem = true, AppCategoryId = catCompany?.Id },
                
                // Задачи
                new AppDefinition { Name = "Все задачи", EntityCode = "TASK", Icon = "check2-all", IsSystem = true, AppCategoryId = catTasks?.Id },
                
                // Справочник услуг (теперь тоже в разделе Компания)
                new AppDefinition { Name = "Услуги", EntityCode = "ServiceItem", Icon = "cart-check", IsSystem = true, AppCategoryId = catCompany?.Id },
                new AppDefinition { Name = "Категории услуг", EntityCode = "ServiceCategory", Icon = "folder2", IsSystem = true, AppCategoryId = catCompany?.Id }
            };

            foreach (var app in systemApps)
            {
                var existingApp = await context.AppDefinitions.FirstOrDefaultAsync(a => a.EntityCode == app.EntityCode);
                if (existingApp == null)
                {
                    app.Id = Guid.NewGuid();
                    context.AppDefinitions.Add(app);
                }
                else
                {
                    existingApp.IsSystem = true; 
                    existingApp.Name = app.Name;
                    existingApp.Icon = app.Icon;
                    existingApp.AppCategoryId = app.AppCategoryId;
                }
            }

            await context.SaveChangesAsync();
        }
    }
}