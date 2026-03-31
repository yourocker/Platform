using System;
using System.Linq.Expressions;
using System.Reflection;
using Core.Entities.Company;
using Core.Entities.Platform;
using Core.Entities.Tasks;
using Core.Entities.CRM;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Core.Entities.System;
using Core.Data.Interceptors;
using Core.Data.Extensions;
using Core.Entities;
using Core.Entities.Platform.Form;
using Core.MultiTenancy;

namespace Core.Data
{
    public class AppDbContext : IdentityDbContext<Employee, IdentityRole<Guid>, Guid>
    {
        private readonly ITenantContextAccessor? _tenantContextAccessor;
        private int _hardDeleteScopeDepth;

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            ITenantContextAccessor? tenantContextAccessor = null) : base(options)
        {
            _tenantContextAccessor = tenantContextAccessor;
            // Настройка для корректной работы с датами в PostgreSQL
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public Guid? CurrentTenantId => _tenantContextAccessor?.CurrentTenant?.Id;
        private bool IsTenantFilterEnabled => CurrentTenantId.HasValue;

        // --- Инфраструктура платформы ---
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<AppDefinition> AppDefinitions { get; set; }
        public DbSet<AppFieldDefinition> AppFieldDefinitions { get; set; }
        public DbSet<AppCategory> AppCategories { get; set; }
        
        // ---Таблица форм---
        public DbSet<AppFormDefinition> AppFormDefinitions { get; set; }

        // --- CRM (Изолированные таблицы для высокой нагрузки) ---
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<ContactPhone> ContactPhones { get; set; }
        public DbSet<ContactEmail> ContactEmails { get; set; }
        
        // --- Оргструктура ---
        public DbSet<Employee> Employees { get; set; }
        public DbSet<EmployeeTenantMembership> EmployeeTenantMemberships { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<StaffAppointment> StaffAppointments { get; set; }
        
        // --- Универсальное хранилище (только для мелких кастомных сущностей) ---
        public DbSet<GenericObject> GenericObjects { get; set; }
        
        // --- Задачи и события ---
        public DbSet<EmployeeTask> EmployeeTasks { get; set; }
        public DbSet<TaskComment> TaskComments { get; set; }
        public DbSet<TaskEntityRelation> TaskEntityRelations { get; set; }
        public DbSet<OutboxEvent> OutboxEvents { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        
        // --- Рабочие графики ---
        public DbSet<CompanyWorkMode> CompanyWorkModes { get; set; }
        public DbSet<CompanyHoliday> CompanyHolidays { get; set; }
        public DbSet<EmployeeSchedule> EmployeeSchedules { get; set; }
        
        // --- Список товаров/услуг ---
        public DbSet<ServiceCategory> ServiceCategories { get; set; }
        public DbSet<ServiceItem> ServiceItems { get; set; }
        
        // --- Таблица настроек интерфейса ---
        public DbSet<UiSettings> UiSettings { get; set; }
        public DbSet<FeatureToggle> FeatureToggles { get; set; }
        public DbSet<BookingPolicySettings> BookingPolicySettings { get; set; }
        public DbSet<BookingStatus> BookingStatuses { get; set; }
        public DbSet<UserFilterPreset> UserFilterPresets { get; set; }
        public DbSet<StoredFile> StoredFiles { get; set; }
        
        // --- CRM ---
        public DbSet<CrmPipeline> CrmPipelines { get; set; }
        public DbSet<CrmStage> CrmStages { get; set; }
        public DbSet<Lead> Leads { get; set; }
        public DbSet<Deal> Deals { get; set; }
        public DbSet<CrmResource> CrmResources { get; set; }
        public DbSet<CrmResourceBooking> CrmResourceBookings { get; set; }
        public DbSet<CrmResourceBookingItem> CrmResourceBookingItems { get; set; }
        public DbSet<CrmResourceBookingContact> CrmResourceBookingContacts { get; set; }
        public DbSet<CrmDealItem> CrmDealItems { get; set; }
        public DbSet<CrmEvent> CrmEvents { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(new OutboxInterceptor());
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            var methodInfo = typeof(NpgsqlJsonExtensions)
                .GetMethod(nameof(NpgsqlJsonExtensions.JsonExtractPathText), new[] { typeof(string), typeof(string) });

            if (methodInfo != null)
            {
                modelBuilder
                    .HasDbFunction(methodInfo)
                    .HasName("jsonb_extract_path_text") // Имя функции в Postgres
                    .IsBuiltIn(true);
            }
            
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Key).IsUnique();
                entity.Property(e => e.Key).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Notes).HasMaxLength(1024);
            });

            modelBuilder.Entity<StoredFile>(entity =>
            {
                entity.ToTable("StoredFiles");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TenantId, e.RelativePath }).IsUnique();
                entity.HasIndex(e => new { e.TenantId, e.OwnerEntityCode, e.OwnerEntityId });
                entity.Property(e => e.RelativePath).HasMaxLength(2048).IsRequired();
                entity.Property(e => e.OriginalFileName).HasMaxLength(255).IsRequired();
                entity.Property(e => e.StoredFileName).HasMaxLength(255).IsRequired();
                entity.Property(e => e.ContentType).HasMaxLength(255);
                entity.Property(e => e.Category).HasMaxLength(128);
                entity.Property(e => e.OwnerEntityCode).HasMaxLength(128);
            });

            // 1. Настройка Контактов (Явная изоляция от GenericObjects)
            modelBuilder.Entity<Contact>(entity =>
            {
                entity.ToTable("Contacts"); // ФИЗИЧЕСКИ отдельная таблица
                
                // Поддержка JSONB для динамических свойств (для скорости в Postgres)
                entity.Property(e => e.Properties).HasColumnType("jsonb");
                
                // Индексы для мгновенного поиска в миллионной базе
                entity.HasIndex(e => e.LastName);
                entity.HasIndex(e => e.FirstName);
                entity.HasIndex(e => e.FullName);

                entity.HasMany(c => c.Phones)
                    .WithOne(p => p.Contact)
                    .HasForeignKey(p => p.ContactId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.Emails)
                    .WithOne(e => e.Contact)
                    .HasForeignKey(e => e.ContactId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 2. Настройка телефонов и почты (Отдельные таблицы)
            modelBuilder.Entity<ContactPhone>(entity =>
            {
                entity.ToTable("ContactPhones");
                entity.HasIndex(p => p.Number); // Индекс для поиска по номеру
            });

            modelBuilder.Entity<ContactEmail>(entity =>
            {
                entity.ToTable("ContactEmails");
                entity.HasIndex(e => e.Email); // Индекс для поиска по почте
            });

            // 3. Базовая таблица для динамических объектов (TPT)
            modelBuilder.Entity<GenericObject>().ToTable("GenericObjects");

            // 4. Сотрудники (Изоляция от Identity)
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.ToTable("Employees");
                entity.Property(e => e.Phones).HasColumnType("jsonb");
                entity.Property(e => e.Emails).HasColumnType("jsonb");
                entity.Property(e => e.Properties).HasColumnType("jsonb");
                entity.Ignore(e => e.FullName);

                entity.HasMany(e => e.TenantMemberships)
                    .WithOne(m => m.Employee)
                    .HasForeignKey(m => m.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EmployeeTenantMembership>(entity =>
            {
                entity.ToTable("EmployeeTenantMemberships");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TenantId, e.EmployeeId }).IsUnique();
                entity.HasIndex(e => new { e.EmployeeId, e.IsActive });
                entity.Property(e => e.RoleCode).HasMaxLength(64).IsRequired();

                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 5. Задачи (Отдельная таблица)
            modelBuilder.Entity<EmployeeTask>(entity =>
            {
                entity.ToTable("EmployeeTasks");
                
                entity.HasOne(t => t.Author)
                    .WithMany()
                    .HasForeignKey(t => t.AuthorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Assignee)
                    .WithMany()
                    .HasForeignKey(t => t.AssigneeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<UserNotification>(entity =>
            {
                entity.ToTable("UserNotifications");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TenantId, e.UserId });
                entity.HasIndex(e => new { e.TenantId, e.SourceEventId }).IsUnique();
                entity.Property(e => e.Title).HasMaxLength(256).IsRequired();
                entity.Property(e => e.Message).HasMaxLength(2000).IsRequired();
                entity.Property(e => e.Url).HasMaxLength(1024).IsRequired();
            });

            // 6. Оргструктура
            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasOne(d => d.Parent)
                    .WithMany(d => d.Children)
                    .HasForeignKey(d => d.ParentId);
            });

            // 7. Уникальный индекс для метаданных полей
            modelBuilder.Entity<AppFieldDefinition>(entity =>
            {
                entity.HasIndex(f => new { f.TenantId, f.AppDefinitionId, f.SystemName }).IsUnique();
            });

            modelBuilder.Entity<AppCategory>(entity =>
            {
                entity.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
            });

            modelBuilder.Entity<AppDefinition>(entity =>
            {
                entity.HasIndex(e => new { e.TenantId, e.EntityCode }).IsUnique();
            });
            
            // 8. Рабочие графики
            modelBuilder.Entity<CompanyWorkMode>().ToTable("CompanyWorkModes");
            modelBuilder.Entity<CompanyHoliday>().ToTable("CompanyHolidays");
            modelBuilder.Entity<EmployeeSchedule>().ToTable("EmployeeSchedules");
            
            modelBuilder.Entity<EmployeeSchedule>()
                .HasOne(s => s.Employee)
                .WithMany()
                .HasForeignKey(s => s.EmployeeId);
            
            // 9. Справочник услуг (Прайс-лист)
            modelBuilder.Entity<ServiceCategory>(entity =>
            {
                entity.ToTable("ServiceCategories");
                entity.Property(e => e.Properties).HasColumnType("jsonb");

                entity.Property(e => e.Name).HasMaxLength(255).IsRequired();

                entity.HasOne(c => c.ParentCategory)
                    .WithMany(c => c.Children)
                    .HasForeignKey(c => c.ParentCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ServiceItem>(entity =>
            {
                entity.ToTable("ServiceItems");
                entity.Property(e => e.Properties).HasColumnType("jsonb");

                entity.Property(e => e.Name).HasMaxLength(255).IsRequired();

                entity.HasIndex(e => e.Name);
                entity.HasOne(s => s.Category)
                    .WithMany(c => c.Services)
                    .HasForeignKey(s => s.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            // 10. Таблица настроек интерфейса
            modelBuilder.Entity<UiSettings>(entity =>
            {
                entity.ToTable("UiSettings");
                entity.HasIndex(e => new { e.TenantId, e.EmployeeId });
            });

            // 10.1. Переключатели модулей (тарифы / лицензии)
            modelBuilder.Entity<FeatureToggle>(entity =>
            {
                entity.ToTable("FeatureToggles");
                entity.HasIndex(e => new { e.TenantId, e.FeatureCode }).IsUnique();
                entity.Property(e => e.FeatureCode).HasMaxLength(64).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(256);
            });

            // 10.2. Глобальная политика бронирования
            modelBuilder.Entity<BookingPolicySettings>(entity =>
            {
                entity.ToTable("BookingPolicySettings");
            });

            modelBuilder.Entity<BookingStatus>(entity =>
            {
                entity.ToTable("BookingStatuses");
                entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
                entity.Property(e => e.Category).HasConversion<int>();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.IsActive);
            });

            // 10.3. Сохраненные фильтры пользователей (привязка к сущности/представлению)
            modelBuilder.Entity<UserFilterPreset>(entity =>
            {
                entity.ToTable("UserFilterPresets");
                entity.Property(e => e.EntityCode).HasMaxLength(64).IsRequired();
                entity.Property(e => e.ViewCode).HasMaxLength(64).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
                entity.Property(e => e.FiltersJson).HasColumnType("jsonb").IsRequired();

                entity.HasIndex(e => new { e.TenantId, e.UserId, e.EntityCode, e.ViewCode });
                entity.HasIndex(e => new { e.TenantId, e.UserId, e.EntityCode, e.ViewCode, e.Name }).IsUnique();

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            // 11. Настройка CRM: Воронки и Этапы
            modelBuilder.Entity<CrmPipeline>(entity =>
            {
                entity.ToTable("CrmPipelines");
                entity.HasKey(e => e.Id);
                
                entity.HasMany(p => p.Stages)
                    .WithOne(s => s.Pipeline)
                    .HasForeignKey(s => s.PipelineId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CrmStage>(entity =>
            {
                entity.ToTable("CrmStages");
                entity.HasIndex(e => e.PipelineId);
            });

            // 13. Настройка CRM: Лиды
            modelBuilder.Entity<Lead>(entity =>
            {
                entity.ToTable("CrmLeads");
                
                // Наследование от GenericObject требует сохранения поддержки JSONB
                entity.Property(e => e.Properties).HasColumnType("jsonb");
                
                entity.HasIndex(e => e.StageId);
                entity.HasIndex(e => e.ResponsibleId);
            });

            // 14. Настройка CRM: Сделки
            modelBuilder.Entity<Deal>(entity =>
            {
                entity.ToTable("CrmDeals");
                
                entity.Property(e => e.Properties).HasColumnType("jsonb");
                
                entity.HasIndex(e => e.StageId);
                entity.HasIndex(e => e.ResponsibleId);
                entity.HasIndex(e => e.ContactId);
            });
            
            // 15. Настройка CRM: ресурсы и бронирования
            modelBuilder.Entity<CrmResource>(entity =>
            {
                entity.ToTable("CrmResources");
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.IsActive);
            });

            modelBuilder.Entity<CrmResourceBooking>(entity =>
            {
                entity.ToTable("CrmResourceBookings");
                entity.Property(e => e.Properties).HasColumnType("jsonb");
                entity.Property(e => e.Title).HasColumnType("text");
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountReason).HasColumnType("text");

                entity.HasIndex(e => new { e.ResourceId, e.StartTime, e.EndTime });
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.StartTime);
                entity.HasIndex(e => e.PerformerEmployeeId);
                entity.HasIndex(e => e.CreatedByEmployeeId);
                entity.HasIndex(e => e.ServiceItemId);
                entity.HasIndex(e => e.StatusId);

                entity.HasOne(e => e.PerformerEmployee)
                    .WithMany()
                    .HasForeignKey(e => e.PerformerEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedByEmployee)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedByEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ServiceItem)
                    .WithMany()
                    .HasForeignKey(e => e.ServiceItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Status)
                    .WithMany()
                    .HasForeignKey(e => e.StatusId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(e => e.BookingItems)
                    .WithOne(i => i.Booking)
                    .HasForeignKey(i => i.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.BookingContacts)
                    .WithOne(i => i.Booking)
                    .HasForeignKey(i => i.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CrmResourceBookingItem>(entity =>
            {
                entity.ToTable("CrmResourceBookingItems");
                entity.Property(e => e.Quantity).HasColumnType("decimal(18,2)");
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CustomUnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");

                entity.HasIndex(e => e.BookingId);
                entity.HasIndex(e => e.ServiceItemId);

                entity.HasOne(e => e.ServiceItem)
                    .WithMany()
                    .HasForeignKey(e => e.ServiceItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CrmResourceBookingContact>(entity =>
            {
                entity.ToTable("CrmResourceBookingContacts");
                entity.HasKey(e => new { e.BookingId, e.ContactId });
                entity.HasIndex(e => e.ContactId);

                entity.HasOne(e => e.Contact)
                    .WithMany()
                    .HasForeignKey(e => e.ContactId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CrmDealItem>(entity =>
            {
                entity.ToTable("CrmDealItems");

                entity.HasOne(e => e.Deal)
                    .WithMany(d => d.Items)
                    .HasForeignKey(e => e.DealId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Исторические позиции сделки не должны удаляться каскадно при удалении услуги из справочника.
                entity.HasOne(e => e.ServiceItem)
                    .WithMany()
                    .HasForeignKey(e => e.ServiceItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            
            // 16. События в CRM
            modelBuilder.Entity<CrmEvent>(entity => {
                entity.ToTable("CrmEvents");
                entity.HasIndex(e => new { e.TargetId, e.TargetEntityCode }); // Для быстрого поиска истории карточки
                entity.HasIndex(e => e.CreatedAt);
            });
            
            // 17. Настройка форм
            modelBuilder.Entity<AppFormDefinition>(entity =>
            {
                // Индекс для быстрого поиска стандартной формы
                entity.HasIndex(f => new { f.TenantId, f.AppDefinitionId, f.Type, f.IsDefault }); 
                
                // Уникальный индекс: Нельзя создать две формы с одинаковым именем для одной сущности и типа задачи
                entity.HasIndex(f => new { f.TenantId, f.AppDefinitionId, f.Type, f.Name }).IsUnique();

                entity.Property(e => e.Layout).HasColumnType("jsonb");
            });

            ConfigureTenantFilters(modelBuilder);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplySoftDeleteScope();
            ApplyTenantScope();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplySoftDeleteScope();
            ApplyTenantScope();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public IDisposable BeginHardDeleteScope()
        {
            _hardDeleteScopeDepth++;
            return new HardDeleteScope(this);
        }

        private void ApplySoftDeleteScope()
        {
            if (_hardDeleteScopeDepth > 0)
            {
                return;
            }

            foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
            {
                if (entry.State != EntityState.Deleted)
                {
                    continue;
                }

                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
            }
        }

        private void ApplyTenantScope()
        {
            if (!CurrentTenantId.HasValue)
            {
                return;
            }

            var tenantId = CurrentTenantId.Value;

            foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
            {
                if (entry.Entity is Tenant)
                {
                    continue;
                }

                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity.TenantId == Guid.Empty)
                    {
                        entry.Entity.TenantId = tenantId;
                    }
                    else if (entry.Entity.TenantId != tenantId)
                    {
                        throw new InvalidOperationException("Попытка сохранить сущность в tenant, отличный от текущего контекста.");
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (entry.Entity.TenantId == Guid.Empty)
                    {
                        entry.Entity.TenantId = tenantId;
                    }
                    else if (entry.Entity.TenantId != tenantId)
                    {
                        throw new InvalidOperationException("Попытка изменить сущность другого tenant.");
                    }
                }
            }
        }

        private void ConfigureTenantFilters(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;
                if (clrType == null || clrType == typeof(Tenant))
                {
                    continue;
                }

                var isTenantEntity = typeof(ITenantEntity).IsAssignableFrom(clrType);
                var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);
                if (!isTenantEntity && !isSoftDeletable)
                {
                    continue;
                }

                var baseClrType = entityType.BaseType?.ClrType;
                if (baseClrType != null &&
                    (typeof(ITenantEntity).IsAssignableFrom(baseClrType) ||
                     typeof(ISoftDeletable).IsAssignableFrom(baseClrType)))
                {
                    continue;
                }

                var method = typeof(AppDbContext)
                    .GetMethod(nameof(SetVisibilityFilter), BindingFlags.Instance | BindingFlags.NonPublic)?
                    .MakeGenericMethod(clrType);

                method?.Invoke(this, new object[] { modelBuilder });
            }

            modelBuilder.Entity<Employee>().HasQueryFilter(BuildEmployeeTenantFilter());
        }

        private void SetVisibilityFilter<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class
        {
            var entityType = typeof(TEntity);
            var isTenantEntity = typeof(ITenantEntity).IsAssignableFrom(entityType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(entityType);

            if (isTenantEntity && isSoftDeletable)
            {
                ApplyTypedFilter(nameof(ApplyTenantSoftDeleteFilter), entityType, modelBuilder);
                return;
            }

            if (isTenantEntity)
            {
                ApplyTypedFilter(nameof(ApplyTenantFilter), entityType, modelBuilder);
                return;
            }

            ApplyTypedFilter(nameof(ApplySoftDeleteFilter), entityType, modelBuilder);
        }

        private void ApplyTypedFilter(string methodName, Type entityType, ModelBuilder modelBuilder)
        {
            var method = typeof(AppDbContext)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?
                .MakeGenericMethod(entityType);

            method?.Invoke(this, new object[] { modelBuilder });
        }

        private void ApplyTenantSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class, ITenantEntity, ISoftDeletable
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(BuildTenantSoftDeleteFilter<TEntity>());
        }

        private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class, ITenantEntity
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(BuildTenantFilter<TEntity>());
        }

        private void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class, ISoftDeletable
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(BuildSoftDeleteFilter<TEntity>());
        }

        private Expression<Func<TEntity, bool>> BuildTenantSoftDeleteFilter<TEntity>()
            where TEntity : class, ITenantEntity, ISoftDeletable
        {
            return entity => (!IsTenantFilterEnabled || entity.TenantId == CurrentTenantId.GetValueOrDefault()) && !entity.IsDeleted;
        }

        private Expression<Func<TEntity, bool>> BuildTenantFilter<TEntity>()
            where TEntity : class, ITenantEntity
        {
            return entity => !IsTenantFilterEnabled || entity.TenantId == CurrentTenantId.GetValueOrDefault();
        }

        private Expression<Func<TEntity, bool>> BuildSoftDeleteFilter<TEntity>()
            where TEntity : class, ISoftDeletable
        {
            return entity => !entity.IsDeleted;
        }

        private Expression<Func<Employee, bool>> BuildEmployeeTenantFilter()
        {
            return employee => !IsTenantFilterEnabled ||
                employee.TenantMemberships.Any(m =>
                    m.TenantId == CurrentTenantId.GetValueOrDefault() &&
                    m.IsActive);
        }

        private sealed class HardDeleteScope : IDisposable
        {
            private readonly AppDbContext _context;
            private bool _disposed;

            public HardDeleteScope(AppDbContext context)
            {
                _context = context;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                if (_context._hardDeleteScopeDepth > 0)
                {
                    _context._hardDeleteScopeDepth--;
                }

                _disposed = true;
            }
        }
    }
}
