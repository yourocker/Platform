using System;
using Microsoft.EntityFrameworkCore;
using MedicalBot.Entities; 
using MedicalBot.Entities.Company; // Добавляем этот using!

namespace MedicalBot.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        // Базовые таблицы
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Visit> Visits { get; set; }
        public DbSet<Appointment> Appointments { get; set; } // Это старые записи приемов

        // Таблицы структуры компании (Module: Company)
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<StaffAppointment> StaffAppointments { get; set; } // Наше новое имя

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка JSONB для Employee
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.Property(e => e.Contacts).HasColumnType("jsonb");
                entity.Property(e => e.Properties).HasColumnType("jsonb");
            });

            // Настройка иерархии отделов
            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasOne(d => d.Parent)
                    .WithMany(d => d.Children)
                    .HasForeignKey(d => d.ParentId);

                // Явно указываем связь с руководителем
                entity.HasOne(d => d.Manager)
                    .WithMany()
                    .HasForeignKey(d => d.ManagerId);
            });

            // Индекс для пациентов
            modelBuilder.Entity<Patient>()
                .HasIndex(p => p.NormalizedName);
        }
    }
}