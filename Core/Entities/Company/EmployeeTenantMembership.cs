using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Entities.System;
using Core.MultiTenancy;

namespace Core.Entities.Company
{
    public class EmployeeTenantMembership : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        [ForeignKey(nameof(TenantId))]
        public Tenant Tenant { get; set; } = null!;

        public Guid EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee Employee { get; set; } = null!;

        [MaxLength(64)]
        public string RoleCode { get; set; } = "admin";

        public bool IsActive { get; set; } = true;

        public bool IsDismissed { get; set; }

        public DateTime? DismissedAt { get; set; }

        public bool IsDefault { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
