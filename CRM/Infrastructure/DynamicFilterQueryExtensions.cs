using Core.Data.Extensions;
using Core.Entities.Platform;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure;

public static class DynamicFilterQueryExtensions
{
    public static IQueryable<TEntity> ApplyDynamicPropertyFilter<TEntity>(
        this IQueryable<TEntity> query,
        string jsonPropertyName,
        AppFieldDefinition field,
        string? rawValue)
        where TEntity : class
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return query;
        }

        var value = rawValue.Trim();
        var normalizedNumeric = value.Replace(",", ".");

        return field.DataType switch
        {
            FieldDataType.Boolean => query.Where(entity =>
                NpgsqlJsonExtensions.JsonExtractPathText(EF.Property<string>(entity, jsonPropertyName), field.SystemName) ==
                NormalizeBooleanValue(value)),

            FieldDataType.Select or FieldDataType.EntityLink => query.Where(entity =>
                NpgsqlJsonExtensions.JsonExtractPathText(EF.Property<string>(entity, jsonPropertyName), field.SystemName) == value),

            FieldDataType.Date or FieldDataType.DateTime => query.Where(entity =>
                NpgsqlJsonExtensions.JsonExtractPathText(EF.Property<string>(entity, jsonPropertyName), field.SystemName) != null &&
                NpgsqlJsonExtensions.JsonExtractPathText(EF.Property<string>(entity, jsonPropertyName), field.SystemName).StartsWith(value)),

            FieldDataType.Number or FieldDataType.Money => query.Where(entity =>
                EF.Functions.ILike(
                    NpgsqlJsonExtensions.JsonExtractPathText(EF.Property<string>(entity, jsonPropertyName), field.SystemName),
                    $"%{normalizedNumeric}%")),

            _ => query.Where(entity =>
                EF.Functions.ILike(
                    NpgsqlJsonExtensions.JsonExtractPathText(EF.Property<string>(entity, jsonPropertyName), field.SystemName),
                    $"%{value}%"))
        };
    }

    private static string NormalizeBooleanValue(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            ? "true"
            : "false";
    }
}
