using Core.Entities.Platform;
using Core.Entities.CRM;
using Core.Entities.Company;
using Core.Entities.System;
using Core.Constants;
using Core.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Core.Data
{
    public static class DbInitializer
    {
        private const string BookingEntityCode = "ResourceBooking";
        public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public static async Task<TenantInfo> EnsureDefaultTenantAsync(AppDbContext context, IConfiguration configuration)
        {
            var tenantKey = configuration[$"{TenantResolutionOptions.SectionName}:DefaultTenantKey"] ?? "default";
            var tenantName = configuration[$"{TenantResolutionOptions.SectionName}:DefaultTenantName"] ?? "Default Company";

            var tenant = await context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Key == tenantKey);

            if (tenant == null)
            {
                tenant = new Tenant
                {
                    Id = DefaultTenantId,
                    Key = tenantKey,
                    Name = tenantName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.Tenants.Add(tenant);
                await context.SaveChangesAsync();
            }
            else if (!tenant.IsActive || !string.Equals(tenant.Name, tenantName, StringComparison.Ordinal))
            {
                tenant.IsActive = true;
                tenant.Name = tenantName;
                await context.SaveChangesAsync();
            }

            return new TenantInfo
            {
                Id = tenant.Id,
                Key = tenant.Key,
                Name = tenant.Name,
                IsActive = tenant.IsActive
            };
        }

        public static async Task Initialize(AppDbContext context, UserManager<Employee> userManager, IConfiguration configuration)
        {
            // --- ШАГ 1: Категории меню ---
            var catCRM = await EnsureCategoriesAsync(context);

            // --- ШАГ 2: Системные приложения (Определения) ---
            await EnsureAppDefinitionsAsync(context, catCRM);

            // --- ШАГ 3: Переключатели модулей (под тарифы) ---
            await EnsureFeatureTogglesAsync(context);

            // --- ШАГ 4: Глобальная политика бронирования ---
            await EnsureBookingPolicyAsync(context);

            // --- ШАГ 5: Обязательное системное поле "Название" для пользовательских сущностей ---
            await EnsureNameFieldsAsync(context);

            // --- ШАГ 6: Базовые формы (Create/Edit/View) с полем "Название" ---
            await EnsureDefaultFormsAsync(context);

            // --- ШАГ 7: Специфические поля для CRM ---
            await EnsureCrmFieldsAsync(context);

            // --- ШАГ 8: Базовые воронки и этапы ---
            await EnsureDefaultPipelinesAsync(context);

            // --- ШАГ 9: Создание администратора из конфигурации ---
            await EnsureAdminAsync(context, userManager, configuration);
        }

        private static async Task EnsureAdminAsync(AppDbContext context, UserManager<Employee> userManager, IConfiguration configuration)
        {
            var enableAdminSeed = bool.TryParse(configuration["SeedData:EnableAdminSeed"], out var parsedEnableAdminSeed)
                && parsedEnableAdminSeed;

            if (!enableAdminSeed)
            {
                return;
            }

            var adminLogin = configuration["SeedData:AdminUser"];
            var adminPassword = configuration["SeedData:AdminPassword"];
            var adminEmail = configuration["SeedData:AdminEmail"];

            if (string.IsNullOrWhiteSpace(adminLogin) ||
                string.IsNullOrWhiteSpace(adminPassword) ||
                string.IsNullOrWhiteSpace(adminEmail))
            {
                throw new InvalidOperationException(
                    "Admin seed is enabled, but SeedData:AdminUser, SeedData:AdminPassword and SeedData:AdminEmail are not fully configured.");
            }

            var adminUser = await context.Employees
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.UserName == adminLogin);

            if (adminUser == null)
            {
                var admin = new Employee
                {
                    Id = Guid.NewGuid(),
                    TenantId = DefaultTenantId,
                    UserName = adminLogin,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FirstName = "System",
                    LastName = "Admin",
                    TimezoneId = "Russian Standard Time",
                    Status = Employee.UserStatus.Offline
                };

                var result = await userManager.CreateAsync(admin, adminPassword);
                
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new Exception($"Ошибка создания админа: {errors}");
                }

                await EnsureEmployeeMembershipAsync(context, admin.Id, DefaultTenantId, "admin", isDefault: true);
            }
            else
            {
                if (adminUser.TenantId == Guid.Empty)
                {
                    adminUser.TenantId = DefaultTenantId;
                    await context.SaveChangesAsync();
                }

                await EnsureEmployeeMembershipAsync(context, adminUser.Id, DefaultTenantId, "admin", isDefault: true);
            }
        }

        public static async Task EnsureEmployeeMembershipAsync(
            AppDbContext context,
            Guid employeeId,
            Guid tenantId,
            string roleCode = "employee",
            bool isDefault = false)
        {
            var existingMemberships = await context.EmployeeTenantMemberships
                .IgnoreQueryFilters()
                .Where(x => x.EmployeeId == employeeId)
                .ToListAsync();

            var membership = existingMemberships.FirstOrDefault(x => x.TenantId == tenantId);
            if (membership == null)
            {
                membership = new EmployeeTenantMembership
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employeeId,
                    TenantId = tenantId,
                    RoleCode = string.IsNullOrWhiteSpace(roleCode) ? "employee" : roleCode.Trim().ToLowerInvariant(),
                    IsActive = true,
                    IsDefault = isDefault,
                    JoinedAt = DateTime.UtcNow
                };

                context.EmployeeTenantMemberships.Add(membership);
            }
            else
            {
                membership.IsActive = true;
                membership.RoleCode = string.IsNullOrWhiteSpace(roleCode) ? membership.RoleCode : roleCode.Trim().ToLowerInvariant();
                membership.IsDefault = membership.IsDefault || isDefault;
            }

            if (isDefault)
            {
                foreach (var otherMembership in existingMemberships.Where(x => x.Id != membership.Id))
                {
                    otherMembership.IsDefault = false;
                }
            }

            await context.SaveChangesAsync();
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
                new AppDefinition { Name = "Бронирования", EntityCode = BookingEntityCode, Icon = "calendar2-check", IsSystem = true, AppCategoryId = catCompany?.Id },
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

        private static async Task EnsureFeatureTogglesAsync(AppDbContext context)
        {
            var defaults = new List<FeatureToggle>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FeatureCode = PlatformFeatures.Crm,
                    IsEnabled = true,
                    Description = "Базовые CRM-сценарии (контакты, лиды, сделки).",
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    FeatureCode = PlatformFeatures.Booking,
                    IsEnabled = true,
                    Description = "Блок бронирования ресурсов.",
                    UpdatedAt = DateTime.UtcNow
                }
            };

            foreach (var item in defaults)
            {
                var existing = await context.FeatureToggles
                    .FirstOrDefaultAsync(x => x.FeatureCode == item.FeatureCode);

                if (existing == null)
                {
                    context.FeatureToggles.Add(item);
                }
                else if (string.IsNullOrWhiteSpace(existing.Description))
                {
                    existing.Description = item.Description;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task EnsureBookingPolicyAsync(AppDbContext context)
        {
            var existing = await context.BookingPolicySettings.FirstOrDefaultAsync();
            if (existing != null)
            {
                return;
            }

            context.BookingPolicySettings.Add(new BookingPolicySettings
            {
                Id = Guid.NewGuid(),
                AllowOverbooking = false,
                MaxParallelBookings = 2,
                AllowManualItemPriceChange = false,
                UpdatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }

        private static async Task EnsureNameFieldsAsync(AppDbContext context)
        {
            var appIds = await context.AppDefinitions
                .Where(a => !a.IsSystem || a.EntityCode == BookingEntityCode)
                .Select(a => a.Id)
                .ToListAsync();

            if (!appIds.Any()) return;

            var existingFields = await context.AppFieldDefinitions
                .Where(f => appIds.Contains(f.AppDefinitionId))
                .Where(f => f.SystemName.ToLower() == "name")
                .ToListAsync();

            foreach (var field in existingFields)
            {
                field.IsSystem = true;
                field.IsRequired = true;
                if (string.IsNullOrWhiteSpace(field.Label)) field.Label = "Название";
            }

            var existing = existingFields.Select(f => f.AppDefinitionId).ToList();
            var missing = appIds.Where(id => !existing.Contains(id)).ToList();
            if (!missing.Any())
            {
                await context.SaveChangesAsync();
                return;
            }

            foreach (var appId in missing)
            {
                context.AppFieldDefinitions.Add(new AppFieldDefinition
                {
                    Id = Guid.NewGuid(),
                    AppDefinitionId = appId,
                    Label = "Название",
                    SystemName = "Name",
                    DataType = FieldDataType.String,
                    IsRequired = true,
                    IsSystem = true,
                    SortOrder = 0
                });
            }

            await context.SaveChangesAsync();
        }

        private static async Task EnsureDefaultFormsAsync(AppDbContext context)
        {
            var appIds = await context.AppDefinitions
                .Select(a => a.Id)
                .ToListAsync();

            if (!appIds.Any()) return;

            var nameFieldMap = await context.AppFieldDefinitions
                .Where(f => appIds.Contains(f.AppDefinitionId))
                .Where(f => f.SystemName.ToLower() == "name")
                .Select(f => new { f.AppDefinitionId, f.Id })
                .ToListAsync();

            var nameFieldByApp = nameFieldMap
                .GroupBy(x => x.AppDefinitionId)
                .ToDictionary(g => g.Key, g => g.First().Id);

            var existingForms = await context.AppFormDefinitions
                .Where(f => appIds.Contains(f.AppDefinitionId))
                .ToListAsync();

            bool changed = false;
            foreach (var appId in appIds)
            {
                if (!nameFieldByApp.TryGetValue(appId, out var nameFieldId)) continue;

                foreach (var type in Enum.GetValues<Core.Entities.Platform.Form.FormType>())
                {
                    var formsOfType = existingForms.Where(f => f.AppDefinitionId == appId && f.Type == type).ToList();
                    if (!formsOfType.Any())
                    {
                        context.AppFormDefinitions.Add(new Core.Entities.Platform.Form.AppFormDefinition
                        {
                            Id = Guid.NewGuid(),
                            AppDefinitionId = appId,
                            Name = "Основная форма",
                            Type = type,
                            IsDefault = true,
                            Layout = BuildNameOnlyLayout(nameFieldId)
                        });
                        changed = true;
                        continue;
                    }

                    if (!formsOfType.Any(f => f.IsDefault))
                    {
                        formsOfType.First().IsDefault = true;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                await context.SaveChangesAsync();
            }
        }

        private static string BuildNameOnlyLayout(Guid nameFieldId)
        {
            return $"{{\"nodes\":[{{\"type\":\"field\",\"FieldId\":\"{nameFieldId}\"}}]}}";
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
