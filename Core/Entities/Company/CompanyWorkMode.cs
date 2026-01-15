using System;
using System.ComponentModel.DataAnnotations;

namespace Core.Entities.Company
{
    // Модель режима работы компании по дням недели
    public class CompanyWorkMode
    {
        [Key]
        public Guid Id { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        
        // Поля для обеда (могут быть пустыми)
        public TimeSpan? LunchStartTime { get; set; }
        public TimeSpan? LunchEndTime { get; set; }
        
        public bool IsWeekend { get; set; }
    }
}