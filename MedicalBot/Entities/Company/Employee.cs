namespace MedicalBot.Entities.Company
{
    public class Employee : IHasDynamicProperties
    {
        public Guid Id { get; set; }
        
        // Поля для ввода
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }

        // Вычисляемое поле (только для чтения в коде)
        public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();

        // Списки контактов
        public List<string> Phones { get; set; } = new();
        public List<string> Emails { get; set; } = new();
        public bool IsDismissed { get; set; } = false; // По умолчанию работает

        public string? Properties { get; set; }
        public List<StaffAppointment> StaffAppointments { get; set; } = new();
    }
}