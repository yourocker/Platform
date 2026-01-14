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
            var categories = new List<AppCategory>
            {
                // Системный раздел CRM
                new AppCategory { Id = Guid.Parse("e5555555-5555-5555-5555-555555555555"), Name = "CRM", Icon = "wallet-fill", SortOrder = 2, IsSystem = true },
                
                new AppCategory { Id = Guid.Parse("a1111111-1111-1111-1111-111111111111"), Name = "Компания", Icon = "building", SortOrder = 1, IsSystem = true },
                new AppCategory { Id = Guid.Parse("b2222222-2222-2222-2222-222222222222"), Name = "Пациенты", Icon = "people", SortOrder = 3, IsSystem = true },
                new AppCategory { Id = Guid.Parse("c3333333-3333-3333-3333-333333333333"), Name = "Конструктор", Icon = "gear", SortOrder = 5, IsSystem = true },
                new AppCategory { Id = Guid.Parse("d4444444-4444-4444-4444-444444444444"), Name = "Сервисы", Icon = "grid", SortOrder = 4, IsSystem = true }
            };

            foreach (var cat in categories)
            {
                var existingCat = await context.AppCategories.FirstOrDefaultAsync(x => x.Name == cat.Name);
                if (existingCat == null)
                {
                    context.AppCategories.Add(cat);
                }
                else
                {
                    existingCat.IsSystem = true;
                    existingCat.Icon = cat.Icon;
                    existingCat.SortOrder = cat.SortOrder;
                }
            }
            await context.SaveChangesAsync();

            // --- ШАГ 2: Инициализация системных сущностей (AppDefinitions) ---
            
            var catCompany = await context.AppCategories.FirstOrDefaultAsync(c => c.Name == "Компания");
            var catPatients = await context.AppCategories.FirstOrDefaultAsync(c => c.Name == "Пациенты");
            var catServices = await context.AppCategories.FirstOrDefaultAsync(c => c.Name == "Сервисы");
            var catCRM = await context.AppCategories.FirstOrDefaultAsync(c => c.Name == "CRM"); 

            var systemApps = new List<AppDefinition>
            {
                // Сущность Контакты (Восстановлено AppCategoryId)
                new AppDefinition { Name = "Контакты", EntityCode = "Contact", Icon = "person-lines-fill", IsSystem = true, AppCategoryId = catCRM?.Id },

                new AppDefinition { Name = "Сотрудники", EntityCode = "Employee", Icon = "person-badge", IsSystem = true, AppCategoryId = catCompany?.Id },
                new AppDefinition { Name = "Пациенты", EntityCode = "Patient", Icon = "person-heart", IsSystem = true, AppCategoryId = catPatients?.Id },
                new AppDefinition { Name = "Должности", EntityCode = "Position", Icon = "briefcase", IsSystem = true, AppCategoryId = catCompany?.Id },
                new AppDefinition { Name = "Подразделения", EntityCode = "Department", Icon = "diagram-3", IsSystem = true, AppCategoryId = catCompany?.Id },
                new AppDefinition { Name = "Задачи", EntityCode = "TASK", Icon = "check2-all", IsSystem = true, AppCategoryId = catServices?.Id }
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