using System;
using System.Collections.Generic;

namespace MedicalBot.Entities.Company
{
    public class Department
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Иерархия
        public Guid? ParentId { get; set; }
        public Department? Parent { get; set; }
        public List<Department> Children { get; set; } = new();

        public Guid? ManagerId { get; set; }
        public Employee? Manager { get; set; }

        // Связь с сотрудниками (оставляем ОДИН список с правильным именем)
        public List<StaffAppointment> StaffAppointments { get; set; } = new();
    }
}