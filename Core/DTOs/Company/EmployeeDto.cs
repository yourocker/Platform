using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Core.DTOs.Interfaces;

namespace Core.DTOs.Company
{
    public class EmployeeAppointmentDto
    {
        public Guid? PositionId { get; set; }
        public Guid? DepartmentId { get; set; }
        public string? PositionName { get; set; }
        public string? DepartmentName { get; set; }
    }

    public abstract class EmployeeInputDto : IDynamicValues
    {
        [Required(ErrorMessage = "Фамилия обязательна")]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Имя обязательно")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? MiddleName { get; set; }

        public List<string> Phones { get; set; } = new();
        public List<string> Emails { get; set; } = new();
        public List<EmployeeAppointmentDto> Appointments { get; set; } = new();

        [StringLength(256)]
        public string? Login { get; set; }

        public Dictionary<string, object> DynamicValues { get; set; } = new();

        public string FullName =>
            string.Join(" ", new[] { LastName, FirstName, MiddleName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    public class EmployeeCreateDto : EmployeeInputDto
    {
        public string? Password { get; set; }
    }

    public class EmployeeEditDto : EmployeeInputDto
    {
        [Required]
        public Guid Id { get; set; }

        public bool IsDismissed { get; set; }

        public string? NewPassword { get; set; }

        public bool HasLogin => !string.IsNullOrWhiteSpace(Login);
    }

    public class EmployeeListDto : IDynamicValues
    {
        public Guid Id { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public List<string> Phones { get; set; } = new();
        public bool IsDismissed { get; set; }
        public List<EmployeeAppointmentDto> Appointments { get; set; } = new();
        public Dictionary<string, object> DynamicValues { get; set; } = new();
    }
}
