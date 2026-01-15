using System;
using System.ComponentModel.DataAnnotations;

namespace Core.Entities.Company
{
    // Модель праздничных и выходных дней в году
    public class CompanyHoliday
    {
        [Key]
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
    }
}