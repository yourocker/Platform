using System;
using Core.Entities;
using Core.Entities.Company;
using Core.Entities.ol;
using Core.Entities.Platform;
using Core.Entities.Tasks;
using Core.Entities.CRM;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Core.Entities.System;
using Core.Data.Interceptors;
using Core.Entities.CRM;
using Core.Data.Extensions;
using Core.Entities.Platform.Form;

namespace Core.Data
{
    public class AppDbContext : IdentityDbContext<Employee, IdentityRole<Guid>, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // Настройка для корректной работы с датами в PostgreSQL
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        // --- Инфраструктура платформы ---
        public DbSet<AppDefinition> AppDefinitions { get; set; }
        public DbSet<AppFieldDefinition> AppFieldDefinitions { get; set; }
        public DbSet<AppCategory> AppCategories { get; set; }
        
        // ---Таблица форм---
        public DbSet<AppFormDefinition> AppFormDefinitions { get; set; }

        // --- CRM (Изолированные таблицы для высокой нагрузки) ---
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<ContactPhone> ContactPhones { get; set; }
        public DbSet<ContactEmail> ContactEmails { get; set; }
        
        // --- Медицинский блок ---
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Visit> Visits { get; set; }
        public DbSet<Appointment> Appointments { get; set; } 

        // --- Оргструктура ---
        public DbSet<Employee> Employees { get; set; }
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
        
        // --- Рабочие графики ---
        public DbSet<CompanyWorkMode> CompanyWorkModes { get; set; }
        public DbSet<CompanyHoliday> CompanyHolidays { get; set; }
        public DbSet<EmployeeSchedule> EmployeeSchedules { get; set; }
        
        // --- Список товаров/услуг ---
        public DbSet<ServiceCategory> ServiceCategories { get; set; }
        public DbSet<ServiceItem> ServiceItems { get; set; }
        
        // --- Таблица настроек интерфейса ---
        public DbSet<UiSettings> UiSettings { get; set; }
        
        // --- CRM ---
        public DbSet<CrmPipeline> CrmPipelines { get; set; }
        public DbSet<CrmStage> CrmStages { get; set; }
        public DbSet<Lead> Leads { get; set; }
        public DbSet<Deal> Deals { get; set; }
        public DbSet<CrmResource> CrmResources { get; set; }
        public DbSet<CrmResourceBooking> CrmResourceBookings { get; set; }
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

            // 6. Пациенты
            modelBuilder.Entity<Patient>(entity =>
            {
                entity.ToTable("Patients");
                entity.Property(p => p.Properties).HasColumnType("jsonb");
                entity.HasIndex(p => p.NormalizedName);
            });

            // 7. Оргструктура
            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasOne(d => d.Parent)
                    .WithMany(d => d.Children)
                    .HasForeignKey(d => d.ParentId);
            });

            // 8. Уникальный индекс для метаданных полей
            modelBuilder.Entity<AppFieldDefinition>(entity =>
            {
                entity.HasIndex(f => new { f.AppDefinitionId, f.SystemName }).IsUnique();
            });
            
            // 9. Рабочие графики
            modelBuilder.Entity<CompanyWorkMode>().ToTable("CompanyWorkModes");
            modelBuilder.Entity<CompanyHoliday>().ToTable("CompanyHolidays");
            modelBuilder.Entity<EmployeeSchedule>().ToTable("EmployeeSchedules");
            
            modelBuilder.Entity<EmployeeSchedule>()
                .HasOne(s => s.Employee)
                .WithMany()
                .HasForeignKey(s => s.EmployeeId);
            
            // 10. Справочник услуг (Прайс-лист)
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
            
            // 11. Таблица настроек интерфейса
            modelBuilder.Entity<UiSettings>(entity =>
            {
                entity.ToTable("UiSettings");
                entity.HasIndex(e => e.EmployeeId);
            });
            
            // 12. Настройка CRM: Воронки и Этапы
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
            
            // 15. Настройка CRM: ресурсы и товары
            modelBuilder.Entity<CrmResource>().ToTable("CrmResources");
            modelBuilder.Entity<CrmResourceBooking>().ToTable("CrmResourceBookings");
            modelBuilder.Entity<CrmDealItem>().ToTable("CrmDealItems");
            
            // 16. События в CRM
            modelBuilder.Entity<CrmEvent>(entity => {
                entity.ToTable("CrmEvents");
                entity.HasIndex(e => new { e.TargetId, e.TargetEntityCode }); // Для быстрого поиска истории карточки
                entity.HasIndex(e => e.CreatedAt);
            });
            
            // 17. Настройка форм
            modelBuilder.Entity<AppFormDefinition>(entity =>
            {
                entity.HasIndex(f => new { f.AppDefinitionId, f.Type, f.IsDefault }); // Ускорение поиска дефолтной формы
                entity.Property(e => e.Layout).HasColumnType("jsonb");
            });
        }
    }
}