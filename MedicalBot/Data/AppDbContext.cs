using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using MedicalBot.Entities; 
using MedicalBot.Entities.Company;
using MedicalBot.Entities.Platform;
using MedicalBot.Entities.Tasks;

namespace MedicalBot.Data
{
    public class AppDbContext : IdentityDbContext<Employee, IdentityRole<Guid>, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public DbSet<AppDefinition> AppDefinitions { get; set; }
        public DbSet<AppFieldDefinition> AppFieldDefinitions { get; set; }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Visit> Visits { get; set; }
        public DbSet<Appointment> Appointments { get; set; } 

        public DbSet<Employee> Employees { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<StaffAppointment> StaffAppointments { get; set; }
        
        public DbSet<GenericObject> GenericObjects { get; set; }
        
        public DbSet<EmployeeTask> EmployeeTasks { get; set; }
        public DbSet<TaskComment> TaskComments { get; set; }
        public DbSet<TaskEntityRelation> TaskEntityRelations { get; set; }
        
        public DbSet<AppCategory> AppCategories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<GenericObject>()
                .HasDiscriminator<string>("ObjectType")
                .HasValue<GenericObject>("Base")
                .HasValue<EmployeeTask>("Task");

            modelBuilder.Entity<EmployeeTask>(entity =>
            {
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

            modelBuilder.Entity<TaskComment>(entity =>
            {
                entity.HasOne(t => t.Task)
                    .WithMany(t => t.Comments)
                    .HasForeignKey(t => t.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.Property(e => e.Phones).HasColumnType("jsonb");
                entity.Property(e => e.Emails).HasColumnType("jsonb");
                entity.Property(e => e.Properties).HasColumnType("jsonb");
                entity.Ignore(e => e.FullName);
            });

            modelBuilder.Entity<Patient>(entity =>
            {
                entity.Property(p => p.Properties).HasColumnType("jsonb");
                entity.HasIndex(p => p.NormalizedName);
            });

            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasOne(d => d.Parent)
                    .WithMany(d => d.Children)
                    .HasForeignKey(d => d.ParentId);

                entity.HasOne(d => d.Manager)
                    .WithMany()
                    .HasForeignKey(d => d.ManagerId);
            });
            
            modelBuilder.Entity<AppFieldDefinition>(entity =>
            {
                entity.HasIndex(f => new { f.AppDefinitionId, f.SystemName })
                      .IsUnique();
            });
        }
    }
}