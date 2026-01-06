using System;
using Microsoft.EntityFrameworkCore;
using MedicalBot.Entities; 

namespace MedicalBot.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // Обязательная настройка для времени в PostgreSQL
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        // Наши таблицы
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Visit> Visits { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Appointment> Appointments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Указываем, что колонки должны быть типа jsonb
            modelBuilder.Entity<Employee>()
                .Property(e => e.Contacts)
                .HasColumnType("jsonb");

            modelBuilder.Entity<Employee>()
                .Property(e => e.Properties)
                .HasColumnType("jsonb");

            // Настройка иерархии отделов
            modelBuilder.Entity<Department>()
                .HasOne(d => d.Parent)
                .WithMany(d => d.Children)
                .HasForeignKey(d => d.ParentId);
            // Ускоряем поиск по имени
            modelBuilder.Entity<Patient>()
                .HasIndex(p => p.NormalizedName);
        }
    }
}