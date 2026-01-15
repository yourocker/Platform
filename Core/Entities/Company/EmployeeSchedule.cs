using System;
using System.ComponentModel.DataAnnotations;

namespace Core.Entities.Company
{
    // Модель индивидуального графика сотрудника
    public class EmployeeSchedule
    {
        [Key]
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }
        
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        
        // Если true - это "период отсутствия", если false - "период работы"
        public bool IsAbsence { get; set; }
    }
}