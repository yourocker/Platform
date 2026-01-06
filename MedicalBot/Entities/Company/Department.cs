using System;
using System.Collections.Generic;

namespace MedicalBot.Entities.Company
{
    public class Department
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        // Иерархия
        public Guid? ParentId { get; set; }
        public Department? Parent { get; set; }
        public List<Department> Children { get; set; } = new();

        // Руководитель (может быть любым сотрудником)
        public Guid? ManagerId { get; set; }
        public Employee? Manager { get; set; }

        // Кто числится в отделе
        public List<StaffAppointment> Appointments { get; set; } = new();
    }
}