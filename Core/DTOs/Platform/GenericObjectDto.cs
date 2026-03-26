using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Core.DTOs.Interfaces;

namespace Core.DTOs.Platform
{
    public class GenericObjectDto : IDynamicValues
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Наименование обязательно")]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        public string EntityCode { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public Dictionary<string, object> DynamicValues { get; set; } = new();
    }

    public class GenericObjectListDto : IDynamicValues
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Dictionary<string, object> DynamicValues { get; set; } = new();
    }
}
