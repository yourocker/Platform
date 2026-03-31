using System;
using System.Collections.Generic;
using Core.MultiTenancy;

namespace Core.Entities.Company
{
    public class Department : IHasDynamicProperties, ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        // Иерархия
        public Guid? ParentId { get; set; }
        public Department? Parent { get; set; }
        public List<Department> Children { get; set; } = new();
        public Guid? ManagerId { get; set; }
        public Employee? Manager { get; set; }
        public List<StaffAppointment> StaffAppointments { get; set; } = new();
        public string? Properties { get; set; }
    }
}
