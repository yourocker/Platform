using System;
using System.ComponentModel.DataAnnotations;

namespace CRM.ViewModels
{
    public class UserProfileViewModel
    {
        public Guid Id { get; set; }
        
        [Display(Name = "Фамилия")]
        public string LastName { get; set; }
        
        [Display(Name = "Имя")]
        public string FirstName { get; set; }
        
        [Display(Name = "Отчество")]
        public string MiddleName { get; set; }
        
        [Display(Name = "Email (Логин)")]
        public string Email { get; set; }
        
        [Display(Name = "Телефон")]
        public string PhoneNumber { get; set; }
        
        [Display(Name = "Часовой пояс")]
        public string TimezoneId { get; set; }

        // Настройки уведомлений
        public bool NotifySoundEnabled { get; set; }
        public bool NotifyDesktopEnabled { get; set; }
        public bool IsAdvancedSettings { get; set; }
        public bool NotifyTaskGeneral { get; set; }
        public bool NotifyTaskAssigned { get; set; }
        public bool NotifyTaskComment { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Введите текущий пароль")]
        [DataType(DataType.Password)]
        public string OldPassword { get; set; }

        [Required(ErrorMessage = "Введите новый пароль")]
        [DataType(DataType.Password)]
        [MinLength(4, ErrorMessage = "Пароль должен быть не менее 4 символов")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Пароли не совпадают")]
        public string ConfirmPassword { get; set; }
    }
}