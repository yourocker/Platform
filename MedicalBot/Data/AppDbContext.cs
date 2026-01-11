using System;
using Microsoft.EntityFrameworkCore;
using MedicalBot.Entities; 
using MedicalBot.Entities.Company;
using MedicalBot.Entities.Platform;
using MedicalBot.Entities.Tasks;

namespace MedicalBot.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        // --- ПЛАТФОРМЕННОЕ ЯДРО ---
        public DbSet<AppDefinition> AppDefinitions { get; set; }
        public DbSet<AppFieldDefinition> AppFieldDefinitions { get; set; }

        // --- БАЗОВЫЕ ТАБЛИЦЫ ---
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Visit> Visits { get; set; }
        public DbSet<Appointment> Appointments { get; set; } 

        // --- СТРУКТУРА КОМПАНИИ ---
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<StaffAppointment> StaffAppointments { get; set; }
        
        // --- УНИВЕРСАЛЬНЫЕ ОБЪЕКТЫ ---
        public DbSet<GenericObject> GenericObjects { get; set; }
        
        // --- ЗАДАЧИ ---
        public DbSet<EmployeeTask> EmployeeTasks { get; set; }
        public DbSet<TaskComment> TaskComments { get; set; }
        public DbSet<TaskEntityRelation> TaskEntityRelations { get; set; }
        
        // --- КАТЕГОРИИ ПРИЛОЖЕНИЙ (пункты меню по сути) ---
        public DbSet<AppCategory> AppCategories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Настройка иерархии: привязываем все неопознанные записи к GenericObject
            modelBuilder.Entity<GenericObject>()
                .HasDiscriminator<string>("ObjectType")
                .HasValue<GenericObject>("Base")
                .HasValue<EmployeeTask>("Task");

            // Конфигурация задачи
            modelBuilder.Entity<EmployeeTask>(entity =>
            {
                // Сопоставление CreatedAt, чтобы не было конфликта полей
                entity.Property(t => t.CreatedAt).HasColumnName("CreatedAt");

                entity.HasOne(t => t.Author)
                    .WithMany()
                    .HasForeignKey(t => t.AuthorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Assignee)
                    .WithMany()
                    .HasForeignKey(t => t.AssigneeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<GenericObject>()
                .HasQueryFilter(b => 
                    !(b is EmployeeTask) || !((EmployeeTask)b).IsDeleted);

            // Конфигурация комментариев
            modelBuilder.Entity<TaskComment>(entity =>
            {
                entity.HasOne(t => t.Task)
                    .WithMany(t => t.Comments)
                    .HasForeignKey(t => t.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Настройка Сотрудника
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.Property(e => e.Phones).HasColumnType("jsonb");
                entity.Property(e => e.Emails).HasColumnType("jsonb");
                entity.Property(e => e.Properties).HasColumnType("jsonb");
                entity.Ignore(e => e.FullName);
            });

            // Настройка Пациента
            modelBuilder.Entity<Patient>(entity =>
            {
                entity.Property(p => p.Properties).HasColumnType("jsonb");
                entity.HasIndex(p => p.NormalizedName);
            });

            // Настройка иерархии отделов
            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasOne(d => d.Parent)
                    .WithMany(d => d.Children)
                    .HasForeignKey(d => d.ParentId);

                entity.HasOne(d => d.Manager)
                    .WithMany()
                    .HasForeignKey(d => d.ManagerId);
            });
            
            // Настройка Платформенных определений
            modelBuilder.Entity<AppFieldDefinition>(entity =>
            {
                entity.HasIndex(f => new { f.AppDefinitionId, f.SystemName })
                      .IsUnique();
            });
        }
    }
}