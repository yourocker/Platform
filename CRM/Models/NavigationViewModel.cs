using System.Collections.Generic;
using Core.Entities.Platform;

namespace CRM.Models
{
    public class NavigationViewModel
    {
        public List<AppCategory> CustomCategories { get; set; } = new();
        public AppCategory CrmCategory { get; set; }
        public string Controller { get; set; }
        public string Action { get; set; }
        public string EntityCode { get; set; }

        // Состояния активности разделов
        public bool IsCRMActive { get; set; }
        public bool IsCompanyActive { get; set; }
        public bool IsTasksActive { get; set; }
        public bool IsSettingsActive { get; set; }
    }
}