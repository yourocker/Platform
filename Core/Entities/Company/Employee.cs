using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using Core.Entities;

namespace Core.Entities.Company
{
    public class Employee : IdentityUser<Guid>, IHasDynamicProperties
    {
        public enum UserStatus
        {
            Online,
            Offline
        }
        
        public override Guid Id { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();

        public List<string> Phones { get; set; } = new();
        public List<string> Emails { get; set; } = new();
        public bool IsDismissed { get; set; } = false; 
        public string TimezoneId { get; set; } = "Russian Standard Time";
        
        // Статус и Базовые настройки (Звук/Пуш)
        public UserStatus Status { get; set; } = UserStatus.Offline;
        public bool NotifySoundEnabled { get; set; } = true;
        public bool NotifyDesktopEnabled { get; set; } = true;

        // Переключатель режима настроек
        public bool IsAdvancedSettings { get; set; } = false;

        // Сами настройки событий
        public bool NotifyTaskGeneral { get; set; } = true;    // Для "Обычного" режима
        public bool NotifyTaskAssigned { get; set; } = true;   // Для "Расширенного": Назначение
        public bool NotifyTaskComment { get; set; } = true;    // Для "Расширенного": Комментарии

        public string? Properties { get; set; }
        public List<StaffAppointment> StaffAppointments { get; set; } = new();
    }
}