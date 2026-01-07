using System;

namespace MedicalBot.Entities.Company
{
    public class Position : IHasDynamicProperties
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? SystemName { get; set; }
        public string? Properties { get; set; }
    }
}