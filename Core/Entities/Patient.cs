using System.ComponentModel.DataAnnotations;

namespace Core.Entities
{
    public class Patient
    {
        public Guid Id { get; set; } // Уникальный ID в нашей БД

        [Required]
        public string FullName { get; set; } // ФИО

        public string NormalizedName { get; set; } // Для быстрого поиска (ФИО без пробелов в верхнем регистре)
        
        public string? CardNumber { get; set; } // Номер карты из мастер-базы
        
        public string? PhoneNumber { get; set; } // Номер телефона
        
        public string? Comment { get; set; } // Примечания (например, контакты родственников)
        
        public string? Properties { get; set; } // Хранилище кастомных полей в JSONB

        // Связь: у одного пациента может быть много визитов
        public List<Visit> Visits { get; set; } = new();
    }
}