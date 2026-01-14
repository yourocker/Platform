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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(new OutboxInterceptor());
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
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

            // Уникальный индекс для метаданных полей
            modelBuilder.Entity<AppFieldDefinition>(entity =>
            {
                entity.HasIndex(f => new { f.AppDefinitionId, f.SystemName }).IsUnique();
            });
        }
    }
}