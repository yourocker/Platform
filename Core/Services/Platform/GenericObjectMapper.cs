using System.Collections.Generic;
using System.Text.Json;
using Core.DTOs.Platform;
using Core.Entities.Platform;

namespace Core.Services.Platform
{
    public static class GenericObjectMapper
    {
        private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;

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

        public static GenericObjectDto ToDto(GenericObject entity)
        {
            return new GenericObjectDto
            {
                Id = entity.Id,
                Name = entity.Name,
                EntityCode = entity.EntityCode,
                CreatedAt = entity.CreatedAt,
                DynamicValues = ParseProperties(entity.Properties)
            };
        }

        public static GenericObjectListDto ToListDto(GenericObject entity)
        {
            return new GenericObjectListDto
            {
                Id = entity.Id,
                Name = entity.Name,
                CreatedAt = entity.CreatedAt,
                DynamicValues = ParseProperties(entity.Properties)
            };
        }

        public static GenericObject ToEntity(GenericObjectDto dto, string entityCode)
        {
            return new GenericObject
            {
                Id = dto.Id,
                Name = NormalizeRequired(dto.Name),
                EntityCode = entityCode,
                CreatedAt = dto.CreatedAt
            };
        }

        public static void UpdateEntity(GenericObject entity, GenericObjectDto dto)
        {
            entity.Name = NormalizeRequired(dto.Name);
        }
    }
}
