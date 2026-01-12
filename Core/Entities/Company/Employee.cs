using Microsoft.AspNetCore.Identity;
using Core.Entities;
using Core.Entities.Company;

namespace Core.Entities.Company
{
    // Наследуем от IdentityUser<Guid> для поддержки авторизации
    public class Employee : IdentityUser<Guid>, IHasDynamicProperties
    {
        // Переопределяем Id, чтобы сохранить явное поле в коде. 
        // IdentityUser использует его как первичный ключ.
        public override Guid Id { get; set; }
        
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }

        // Вычисляемое поле (только для чтения в коде)
        public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();

        // Списки контактов
        public List<string> Phones { get; set; } = new();
        public List<string> Emails { get; set; } = new();
        // Признак увольнения
        public bool IsDismissed { get; set; } = false; // По умолчанию 
        
        // Время
        public string TimezoneId { get; set; } = "Russian Standard Time"; // Дефолт: Москва

        public string? Properties { get; set; }
        public List<StaffAppointment> StaffAppointments { get; set; } = new();
    }
}