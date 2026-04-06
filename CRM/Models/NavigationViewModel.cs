using System.Collections.Generic;
using Core.Entities.Platform;

namespace CRM.Models
{
    public class NavigationViewModel
    {
        public List<AppCategory> CustomCategories { get; set; } = new();
        public AppCategory? HomeCategory { get; set; }
        public AppCategory? CrmCategory { get; set; }
        public List<AppDefinition> CrmApps { get; set; } = new();
        public AppCategory? CompanyCategory { get; set; }
        public AppCategory? ScheduleCategory { get; set; }
        public AppCategory? TasksCategory { get; set; }
        public string? Controller { get; set; }
        public string? Action { get; set; }
        public string? EntityCode { get; set; }

        // Состояния активности разделов
        public bool IsCrmModuleEnabled { get; set; } = true;
        public bool IsBookingModuleEnabled { get; set; } = true;
        public bool UseLeads { get; set; } = true;
        public string CrmDefaultUrl { get; set; } = "/Contacts";
        public bool IsHomeActive { get; set; }
        public bool IsCRMActive { get; set; }
        public bool IsScheduleActive { get; set; }
        public bool IsCompanyActive { get; set; }
        public bool IsTasksActive { get; set; }
        public bool IsSettingsActive { get; set; }
    }
}
