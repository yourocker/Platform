using Core.Entities.Platform;
using Core.Entities.CRM; // Добавлено для работы с воронками
using Microsoft.EntityFrameworkCore;

namespace Core.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(AppDbContext context)
        {
            // --- ШАГ 1: Категории меню ---
            var catCRM = await EnsureCategoriesAsync(context);

            // --- ШАГ 2: Системные приложения (Определения) ---
            await EnsureAppDefinitionsAsync(context, catCRM);

            // --- ШАГ 3: Специфические поля для CRM ---
            await EnsureCrmFieldsAsync(context);

            // --- ШАГ 4: Базовые воронки и этапы (ФУНДАМЕНТ ПРОЦЕССОВ) ---
            await EnsureDefaultPipelinesAsync(context);
        }

        private static async Task<AppCategory?> EnsureCategoriesAsync(AppDbContext context)
        {
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
            return await context.AppCategories.FirstOrDefaultAsync(c => c.Name == "CRM");
        }

        private static async Task EnsureAppDefinitionsAsync(AppDbContext context, AppCategory? catCRM)
        {
            var catCompany = await context.AppCategories.FirstOrDefaultAsync(c => c.Name == "Компания");
            var catTasks = await context.AppCategories.FirstOrDefaultAsync(c => c.Name == "Задачи");

            var systemApps = new List<AppDefinition>
            {
                new AppDefinition { Name = "Контакты", EntityCode = "Contact", Icon = "person-lines-fill", IsSystem = true, AppCategoryId = catCRM?.Id },
                new AppDefinition { Name = "Лиды", EntityCode = "Lead", Icon = "person-plus", IsSystem = true, AppCategoryId = catCRM?.Id },
                new AppDefinition { Name = "Сделки", EntityCode = "Deal", Icon = "briefcase", IsSystem = true, AppCategoryId = catCRM?.Id },
                new AppDefinition { Name = "Сотрудники", EntityCode = "Employee", Icon = "person-badge", IsSystem = true, AppCategoryId = catCompany?.Id },
                new AppDefinition { Name = "Должности", EntityCode = "Position", Icon = "briefcase", IsSystem = true, AppCategoryId = catCompany?.Id },
                new AppDefinition { Name = "Подразделения", EntityCode = "Department", Icon = "diagram-3", IsSystem = true, AppCategoryId = catCompany?.Id },
                new AppDefinition { Name = "Услуги", EntityCode = "ServiceItem", Icon = "cart-check", IsSystem = true, AppCategoryId = catCompany?.Id },
                new AppDefinition { Name = "Категории услуг", EntityCode = "ServiceCategory", Icon = "folder2", IsSystem = true, AppCategoryId = catCompany?.Id },
                new AppDefinition { Name = "Все задачи", EntityCode = "TASK", Icon = "check2-all", IsSystem = true, AppCategoryId = catTasks?.Id }
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

        private static async Task EnsureCrmFieldsAsync(AppDbContext context)
        {
            var leadDef = await context.AppDefinitions.FirstOrDefaultAsync(a => a.EntityCode == "Lead");
            if (leadDef == null) return;

            var fieldSystemName = "Source";
            var existingField = await context.AppFieldDefinitions
                .FirstOrDefaultAsync(f => f.AppDefinitionId == leadDef.Id && f.SystemName == fieldSystemName);

            if (existingField == null)
            {
                context.AppFieldDefinitions.Add(new AppFieldDefinition
                {
                    Id = Guid.NewGuid(),
                    AppDefinitionId = leadDef.Id,
                    Label = "Источник", 
                    SystemName = fieldSystemName,
                    DataType = FieldDataType.String,
                    IsRequired = false,
                    SortOrder = 10
                });
                await context.SaveChangesAsync();
            }
        }

        private static async Task EnsureDefaultPipelinesAsync(AppDbContext context)
        {
            // 1. Дефолтная воронка для ЛИДОВ
            if (!await context.CrmPipelines.AnyAsync(p => p.TargetEntityCode == "Lead"))
            {
                var leadPipeline = new CrmPipeline
                {
                    Id = Guid.NewGuid(),
                    Name = "Общая воронка лидов",
                    TargetEntityCode = "Lead",
                    SortOrder = 1
                };
                context.CrmPipelines.Add(leadPipeline);

                context.CrmStages.AddRange(new List<CrmStage>
                {
                    new CrmStage { Id = Guid.NewGuid(), PipelineId = leadPipeline.Id, Name = "Новый", SortOrder = 1, Color = "#007bff", StageType = 0 },
                    new CrmStage { Id = Guid.NewGuid(), PipelineId = leadPipeline.Id, Name = "В работе", SortOrder = 2, Color = "#ffc107", StageType = 0 },
                    new CrmStage { Id = Guid.NewGuid(), PipelineId = leadPipeline.Id, Name = "Сконвертирован", SortOrder = 3, Color = "#28a745", StageType = 1 },
                    new CrmStage { Id = Guid.NewGuid(), PipelineId = leadPipeline.Id, Name = "Некачественный лид", SortOrder = 4, Color = "#dc3545", StageType = 2 }
                });
            }

            // 2. Дефолтная воронка для СДЕЛОК
            if (!await context.CrmPipelines.AnyAsync(p => p.TargetEntityCode == "Deal"))
            {
                var dealPipeline = new CrmPipeline
                {
                    Id = Guid.NewGuid(),
                    Name = "Продажи",
                    TargetEntityCode = "Deal",
                    SortOrder = 1
                };
                context.CrmPipelines.Add(dealPipeline);

                context.CrmStages.AddRange(new List<CrmStage>
                {
                    new CrmStage { Id = Guid.NewGuid(), PipelineId = dealPipeline.Id, Name = "Подготовка документов", SortOrder = 1, Color = "#17a2b8", StageType = 0 },
                    new CrmStage { Id = Guid.NewGuid(), PipelineId = dealPipeline.Id, Name = "Счет на оплату", SortOrder = 2, Color = "#6f42c1", StageType = 0 },
                    new CrmStage { Id = Guid.NewGuid(), PipelineId = dealPipeline.Id, Name = "Сделка успешна", SortOrder = 3, Color = "#28a745", StageType = 1 },
                    new CrmStage { Id = Guid.NewGuid(), PipelineId = dealPipeline.Id, Name = "Сделка провалена", SortOrder = 4, Color = "#343a40", StageType = 2 }
                });
            }

            await context.SaveChangesAsync();
        }
    }
}