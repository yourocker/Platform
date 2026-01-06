using System;
using System.Collections.Generic;

namespace MedicalBot.Entities.Company
{
    public class Employee
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        
        // Данные в формате JSON (телефон, почта и т.д.)
        public string? Contacts { get; set; } 
        
        // Кастомные поля
        public string? Properties { get; set; }

        // Связь с назначениями
        public List<StaffAppointment> Appointments { get; set; } = new();
    }
}