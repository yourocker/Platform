using System;
using Core.MultiTenancy;

namespace Core.Entities.Company
{
    public class Position : IHasDynamicProperties, ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; }
        public string? SystemName { get; set; }
        public string? Properties { get; set; }
    }
}
