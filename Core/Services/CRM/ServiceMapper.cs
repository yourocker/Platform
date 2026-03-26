using System;
using System.Collections.Generic;
using System.Text.Json;
using Core.DTOs.CRM;
using Core.Entities.CRM;

namespace Core.Services.CRM
{
    public static class ServiceMapper
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

        public static ServiceTreeItemDto ToTreeItemDto(ServiceItem item, int level)
        {
            return new ServiceTreeItemDto
            {
                Id = item.Id,
                ParentId = item.CategoryId,
                Name = item.Name,
                Type = "Item",
                Price = item.Price,
                Level = level,
                DynamicValues = ParseProperties(item.Properties)
            };
        }

        public static ServiceTreeItemDto ToTreeItemDto(ServiceCategory category, Guid? parentId, int level)
        {
            return new ServiceTreeItemDto
            {
                Id = category.Id,
                ParentId = parentId,
                Name = category.Name,
                Type = "Category",
                Level = level,
                DynamicValues = ParseProperties(category.Properties)
            };
        }

        public static ServiceItemFormDto ToFormDto(ServiceItem item)
        {
            return new ServiceItemFormDto
            {
                Id = item.Id,
                Name = item.Name,
                CategoryId = item.CategoryId,
                Price = item.Price,
                DynamicValues = ParseProperties(item.Properties)
            };
        }

        public static ServiceCategoryFormDto ToFormDto(ServiceCategory category)
        {
            return new ServiceCategoryFormDto
            {
                Id = category.Id,
                Name = category.Name,
                ParentCategoryId = category.ParentCategoryId,
                DynamicValues = ParseProperties(category.Properties)
            };
        }

        public static ServiceItem ToEntity(ServiceItemFormDto dto)
        {
            return new ServiceItem
            {
                Id = dto.Id,
                Name = dto.Name.Trim(),
                CategoryId = dto.CategoryId ?? Guid.Empty,
                Price = dto.Price
            };
        }

        public static void UpdateEntity(ServiceItem entity, ServiceItemFormDto dto)
        {
            entity.Name = dto.Name.Trim();
            entity.CategoryId = dto.CategoryId ?? Guid.Empty;
            entity.Price = dto.Price;
        }

        public static ServiceCategory ToEntity(ServiceCategoryFormDto dto)
        {
            return new ServiceCategory
            {
                Id = dto.Id,
                Name = dto.Name.Trim(),
                ParentCategoryId = dto.ParentCategoryId
            };
        }

        public static void UpdateEntity(ServiceCategory entity, ServiceCategoryFormDto dto)
        {
            entity.Name = dto.Name.Trim();
            entity.ParentCategoryId = dto.ParentCategoryId;
        }
    }
}
