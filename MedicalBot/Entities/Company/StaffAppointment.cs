using System;

namespace MedicalBot.Entities.Company
{
    public class StaffAppointment
    {
        public Guid Id { get; set; }
        
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; }

        public Guid DepartmentId { get; set; }
        public Department Department { get; set; }

        public Guid PositionId { get; set; }
        public Position Position { get; set; }

        public bool IsPrimary { get; set; }
    }
}