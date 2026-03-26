using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Core.DTOs.Interfaces;

namespace Core.DTOs.CRM
{
    public class ServiceTreeItemDto : IDynamicValues
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Item";
        public decimal? Price { get; set; }
        public int Level { get; set; }
        public Dictionary<string, object> DynamicValues { get; set; } = new();
    }

    public class ServiceItemFormDto : IDynamicValues
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Название обязательно")]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Раздел обязателен")]
        public Guid? CategoryId { get; set; }

        [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Цена должна быть неотрицательной")]
        public decimal Price { get; set; }

        public Dictionary<string, object> DynamicValues { get; set; } = new();
    }

    public class ServiceCategoryFormDto : IDynamicValues
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Название раздела обязательно")]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        public Guid? ParentCategoryId { get; set; }

        public Dictionary<string, object> DynamicValues { get; set; } = new();
    }
}
