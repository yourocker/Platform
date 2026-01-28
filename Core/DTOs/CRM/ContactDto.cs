using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Core.DTOs.Interfaces;

namespace Core.DTOs.CRM
{
    // --- CREATE ---
    public class ContactCreateDto : IDynamicValues
    {
        [Required(ErrorMessage = "Фамилия обязательна")]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Имя обязательно")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? MiddleName { get; set; }

        public List<string> PhoneNumbers { get; set; } = new();
        public List<string> EmailAddresses { get; set; } = new();

        // Реализация интерфейса (инициализируем сразу, чтобы не было null)
        public Dictionary<string, object> DynamicValues { get; set; } = new();
    }

    // --- EDIT ---
    public class ContactEditDto : ContactCreateDto
    {
        [Required]
        public Guid Id { get; set; }
    }

    // --- LIST (INDEX) ---
    public class ContactListDto : IDynamicValues
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public List<string> PhoneNumbers { get; set; } = new();
        public List<string> EmailAddresses { get; set; } = new();
        
        // Нужно для отображения колонок в таблице
        public Dictionary<string, object> DynamicValues { get; set; } = new();
    }

    // --- DETAILS (NEW) ---
    // Создаем отдельный класс для Деталей, чтобы отвязаться от Entity
    public class ContactDetailsDto : IDynamicValues
    {
        public Guid Id { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string FullName { get; set; } = string.Empty;

        public List<string> PhoneNumbers { get; set; } = new();
        public List<string> EmailAddresses { get; set; } = new();

        public Dictionary<string, object> DynamicValues { get; set; } = new();
    }
}