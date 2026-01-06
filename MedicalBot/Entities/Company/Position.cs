using System;

namespace MedicalBot.Entities.Company
{
    public class Position
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? SystemName { get; set; }
    }
}