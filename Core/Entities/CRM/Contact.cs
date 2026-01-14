using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Entities.Platform;

namespace Core.Entities.CRM
{
    [Table("Contacts")]
    public class Contact : GenericObject
    {
        // --- Основные поля ---

        [Required]
        [StringLength(255)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? MiddleName { get; set; }

        // --- Множественные поля (Связи 1-ко-многим) ---
        
        public virtual ICollection<ContactPhone> Phones { get; set; } = new List<ContactPhone>();
        public virtual ICollection<ContactEmail> Emails { get; set; } = new List<ContactEmail>();

        // --- Логика ---

        /// <summary>
        /// Пересчитывает FullName на основе ФИО.
        /// Вызывать перед сохранением, если данные менялись вручную.
        /// </summary>
        public void RecalculateFullName()
        {
            var parts = new List<string?> { LastName, FirstName, MiddleName };
            // Фильтруем пустые части и склеиваем через пробел
            FullName = string.Join(" ", parts.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        }
    }

    [Table("ContactPhones")]
    public class ContactPhone
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Phone]
        [StringLength(50)]
        public string Number { get; set; } = string.Empty;

        // Внешний ключ на контакт
        public Guid ContactId { get; set; }
        
        [ForeignKey("ContactId")]
        public virtual Contact Contact { get; set; } = null!;
    }

    [Table("ContactEmails")]
    public class ContactEmail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        // Внешний ключ на контакт
        public Guid ContactId { get; set; }

        [ForeignKey("ContactId")]
        public virtual Contact Contact { get; set; } = null!;
    }
}