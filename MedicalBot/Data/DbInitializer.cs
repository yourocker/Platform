using MedicalBot.Entities.Platform;
using Microsoft.EntityFrameworkCore;

namespace MedicalBot.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(AppDbContext context)
        {
            // Проверяем, есть ли уже определения. Если есть — выходим.
            if (await context.AppDefinitions.AnyAsync()) return;

            var definitions = new List<AppDefinition>
            {
                new AppDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "Сотрудники",
                    SystemCode = "Employee",
                    IsSystem = true,
                    Icon = "person-badge"
                },
                new AppDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "Пациенты",
                    SystemCode = "Patient",
                    IsSystem = true,
                    Icon = "person-heart"
                },
                new AppDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "Должности",
                    SystemCode = "Position",
                    IsSystem = true,
                    Icon = "briefcase"
                },
                new AppDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = "Подразделения",
                    SystemCode = "Department",
                    IsSystem = true,
                    Icon = "diagram-3"
                }
            };

            context.AppDefinitions.AddRange(definitions);
            await context.SaveChangesAsync();
        }
    }
}