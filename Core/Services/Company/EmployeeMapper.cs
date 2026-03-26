using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Core.DTOs.Company;
using Core.Entities.Company;

namespace Core.Services.Company
{
    public static class EmployeeMapper
    {
        private static Dictionary<string, object> ParseProperties(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                       ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;

        private static string? NormalizeOptional(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public static Employee CreateEntity(EmployeeCreateDto dto)
        {
            return new Employee
            {
                LastName = NormalizeRequired(dto.LastName),
                FirstName = NormalizeRequired(dto.FirstName),
                MiddleName = NormalizeOptional(dto.MiddleName)
            };
        }

        public static void UpdateEntity(Employee employee, EmployeeEditDto dto)
        {
            employee.LastName = NormalizeRequired(dto.LastName);
            employee.FirstName = NormalizeRequired(dto.FirstName);
            employee.MiddleName = NormalizeOptional(dto.MiddleName);
            employee.IsDismissed = dto.IsDismissed;
        }

        public static EmployeeEditDto ToEditDto(Employee employee)
        {
            return new EmployeeEditDto
            {
                Id = employee.Id,
                LastName = employee.LastName,
                FirstName = employee.FirstName,
                MiddleName = employee.MiddleName,
                Phones = employee.Phones?.ToList() ?? new List<string>(),
                Emails = employee.Emails?.ToList() ?? new List<string>(),
                Login = employee.UserName,
                IsDismissed = employee.IsDismissed,
                Appointments = employee.StaffAppointments?
                    .OrderByDescending(appointment => appointment.IsPrimary)
                    .Select(ToAppointmentDto)
                    .ToList() ?? new List<EmployeeAppointmentDto>(),
                DynamicValues = ParseProperties(employee.Properties)
            };
        }

        public static EmployeeListDto ToListDto(Employee employee)
        {
            return new EmployeeListDto
            {
                Id = employee.Id,
                LastName = employee.LastName,
                FirstName = employee.FirstName,
                MiddleName = employee.MiddleName,
                Phones = employee.Phones?.ToList() ?? new List<string>(),
                IsDismissed = employee.IsDismissed,
                Appointments = employee.StaffAppointments?
                    .OrderByDescending(appointment => appointment.IsPrimary)
                    .Select(ToAppointmentDto)
                    .ToList() ?? new List<EmployeeAppointmentDto>(),
                DynamicValues = ParseProperties(employee.Properties)
            };
        }

        public static List<StaffAppointment> ToStaffAppointments(Guid employeeId, IEnumerable<EmployeeAppointmentDto>? appointments)
        {
            return appointments?
                .Where(appointment =>
                    appointment.PositionId.HasValue &&
                    appointment.PositionId.Value != Guid.Empty &&
                    appointment.DepartmentId.HasValue &&
                    appointment.DepartmentId.Value != Guid.Empty)
                .Select((appointment, index) => new StaffAppointment
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employeeId,
                    PositionId = appointment.PositionId!.Value,
                    DepartmentId = appointment.DepartmentId!.Value,
                    IsPrimary = index == 0
                })
                .ToList() ?? new List<StaffAppointment>();
        }

        private static EmployeeAppointmentDto ToAppointmentDto(StaffAppointment appointment)
        {
            return new EmployeeAppointmentDto
            {
                PositionId = appointment.PositionId,
                DepartmentId = appointment.DepartmentId,
                PositionName = appointment.Position?.Name,
                DepartmentName = appointment.Department?.Name
            };
        }
    }
}
