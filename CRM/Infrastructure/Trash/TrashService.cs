using System;
using System.Collections.Generic;
using System.Linq;
using Core.Data;
using Core.Entities.CRM;
using Core.Entities.Platform;
using CRM.ViewModels.CompanySettings;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure.Trash
{
    public class TrashService : ITrashService
    {
        private const string GenericObjectPrefix = "generic";
        private const string BookingPrefix = "booking";
        private const string BookingEntityCode = "ResourceBooking";
        private readonly AppDbContext _context;

        public TrashService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<TrashPageViewModel> GetPageModelAsync(TrashFilterInput filter, CancellationToken cancellationToken = default)
        {
            filter ??= new TrashFilterInput();

            var tenantId = _context.CurrentTenantId;
            if (!tenantId.HasValue)
            {
                return new TrashPageViewModel
                {
                    Filters = filter
                };
            }

            var entityNames = await BuildEntityNameMapAsync(cancellationToken);
            var genericObjects = await BuildGenericObjectQuery(tenantId.Value, filter)
                .Select(entity => new TrashItemViewModel
                {
                    SelectionKey = BuildSelectionKey(GenericObjectPrefix, entity.Id),
                    Id = entity.Id,
                    EntityCode = entity.EntityCode,
                    EntityName = entity.EntityCode,
                    Title = entity.Name,
                    CreatedAt = entity.CreatedAt,
                    DeletedAt = entity.DeletedAt
                })
                .ToListAsync(cancellationToken);

            var bookings = await BuildBookingQuery(tenantId.Value, filter)
                .Select(entity => new TrashItemViewModel
                {
                    SelectionKey = BuildSelectionKey(BookingPrefix, entity.Id),
                    Id = entity.Id,
                    EntityCode = BookingEntityCode,
                    EntityName = BookingEntityCode,
                    Title = entity.Title ?? "Бронирование без названия",
                    CreatedAt = entity.CreatedAt,
                    DeletedAt = entity.DeletedAt
                })
                .ToListAsync(cancellationToken);

            var items = genericObjects
                .Concat(bookings)
                .Select(item =>
                {
                    item.EntityName = entityNames.TryGetValue(item.EntityCode, out var entityName)
                        ? entityName
                        : item.EntityCode;
                    return item;
                })
                .OrderByDescending(item => item.DeletedAt ?? DateTime.MinValue)
                .ThenByDescending(item => item.CreatedAt)
                .ThenBy(item => item.Title)
                .ToList();

            return new TrashPageViewModel
            {
                Filters = filter,
                Items = items,
                TotalCount = items.Count,
                EntityOptions = entityNames
                    .OrderBy(pair => pair.Value)
                    .Select(pair => new TrashEntityOptionViewModel
                    {
                        EntityCode = pair.Key,
                        Name = pair.Value
                    })
                    .ToList(),
                Stats = items
                    .GroupBy(item => new { item.EntityCode, item.EntityName })
                    .Select(group => new TrashEntityStatViewModel
                    {
                        EntityCode = group.Key.EntityCode,
                        EntityName = group.Key.EntityName,
                        Count = group.Count()
                    })
                    .OrderByDescending(stat => stat.Count)
                    .ThenBy(stat => stat.EntityName)
                    .ToList()
            };
        }

        public async Task<int> RestoreAsync(IEnumerable<string>? selectionKeys, CancellationToken cancellationToken = default)
        {
            var tenantId = _context.CurrentTenantId;
            if (!tenantId.HasValue)
            {
                return 0;
            }

            var selections = ParseSelections(selectionKeys);
            var affectedCount = 0;

            if (selections.GenericObjectIds.Any())
            {
                var objects = await _context.GenericObjects
                    .IgnoreQueryFilters()
                    .Where(entity =>
                        entity.TenantId == tenantId.Value &&
                        entity.IsDeleted &&
                        selections.GenericObjectIds.Contains(entity.Id))
                    .ToListAsync(cancellationToken);

                foreach (var entity in objects)
                {
                    entity.IsDeleted = false;
                    entity.DeletedAt = null;
                }

                affectedCount += objects.Count;
            }

            if (selections.BookingIds.Any())
            {
                var bookings = await _context.CrmResourceBookings
                    .IgnoreQueryFilters()
                    .Where(entity =>
                        entity.TenantId == tenantId.Value &&
                        entity.IsDeleted &&
                        selections.BookingIds.Contains(entity.Id))
                    .ToListAsync(cancellationToken);

                foreach (var entity in bookings)
                {
                    entity.IsDeleted = false;
                    entity.DeletedAt = null;
                }

                affectedCount += bookings.Count;
            }

            if (affectedCount > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return affectedCount;
        }

        public async Task<int> PermanentlyDeleteAsync(IEnumerable<string>? selectionKeys, CancellationToken cancellationToken = default)
        {
            var tenantId = _context.CurrentTenantId;
            if (!tenantId.HasValue)
            {
                return 0;
            }

            var selections = ParseSelections(selectionKeys);
            var affectedCount = 0;

            using (_context.BeginHardDeleteScope())
            {
                if (selections.GenericObjectIds.Any())
                {
                    var objects = await _context.GenericObjects
                        .IgnoreQueryFilters()
                        .Where(entity =>
                            entity.TenantId == tenantId.Value &&
                            entity.IsDeleted &&
                            selections.GenericObjectIds.Contains(entity.Id))
                        .ToListAsync(cancellationToken);

                    if (objects.Count > 0)
                    {
                        _context.GenericObjects.RemoveRange(objects);
                        affectedCount += objects.Count;
                    }
                }

                if (selections.BookingIds.Any())
                {
                    var bookings = await _context.CrmResourceBookings
                        .IgnoreQueryFilters()
                        .Where(entity =>
                            entity.TenantId == tenantId.Value &&
                            entity.IsDeleted &&
                            selections.BookingIds.Contains(entity.Id))
                        .ToListAsync(cancellationToken);

                    if (bookings.Count > 0)
                    {
                        _context.CrmResourceBookings.RemoveRange(bookings);
                        affectedCount += bookings.Count;
                    }
                }

                if (affectedCount > 0)
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            return affectedCount;
        }

        private IQueryable<GenericObject> BuildGenericObjectQuery(Guid tenantId, TrashFilterInput filter)
        {
            var query = _context.GenericObjects
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(entity => entity.TenantId == tenantId && entity.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.EntityCode))
            {
                query = query.Where(entity => entity.EntityCode == filter.EntityCode);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var pattern = $"%{filter.Search.Trim()}%";
                query = query.Where(entity => EF.Functions.ILike(entity.Name, pattern));
            }

            if (filter.CreatedFrom.HasValue)
            {
                var createdFrom = filter.CreatedFrom.Value.Date;
                query = query.Where(entity => entity.CreatedAt >= createdFrom);
            }

            if (filter.CreatedTo.HasValue)
            {
                var createdToExclusive = filter.CreatedTo.Value.Date.AddDays(1);
                query = query.Where(entity => entity.CreatedAt < createdToExclusive);
            }

            if (filter.DeletedFrom.HasValue)
            {
                var deletedFrom = filter.DeletedFrom.Value.Date;
                query = query.Where(entity => entity.DeletedAt.HasValue && entity.DeletedAt.Value >= deletedFrom);
            }

            if (filter.DeletedTo.HasValue)
            {
                var deletedToExclusive = filter.DeletedTo.Value.Date.AddDays(1);
                query = query.Where(entity => entity.DeletedAt.HasValue && entity.DeletedAt.Value < deletedToExclusive);
            }

            return query;
        }

        private IQueryable<CrmResourceBooking> BuildBookingQuery(Guid tenantId, TrashFilterInput filter)
        {
            var query = _context.CrmResourceBookings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(entity => entity.TenantId == tenantId && entity.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.EntityCode) &&
                !string.Equals(filter.EntityCode, BookingEntityCode, StringComparison.Ordinal))
            {
                return query.Where(_ => false);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var pattern = $"%{filter.Search.Trim()}%";
                query = query.Where(entity => entity.Title != null && EF.Functions.ILike(entity.Title, pattern));
            }

            if (filter.CreatedFrom.HasValue)
            {
                var createdFrom = filter.CreatedFrom.Value.Date;
                query = query.Where(entity => entity.CreatedAt >= createdFrom);
            }

            if (filter.CreatedTo.HasValue)
            {
                var createdToExclusive = filter.CreatedTo.Value.Date.AddDays(1);
                query = query.Where(entity => entity.CreatedAt < createdToExclusive);
            }

            if (filter.DeletedFrom.HasValue)
            {
                var deletedFrom = filter.DeletedFrom.Value.Date;
                query = query.Where(entity => entity.DeletedAt.HasValue && entity.DeletedAt.Value >= deletedFrom);
            }

            if (filter.DeletedTo.HasValue)
            {
                var deletedToExclusive = filter.DeletedTo.Value.Date.AddDays(1);
                query = query.Where(entity => entity.DeletedAt.HasValue && entity.DeletedAt.Value < deletedToExclusive);
            }

            return query;
        }

        private async Task<Dictionary<string, string>> BuildEntityNameMapAsync(CancellationToken cancellationToken)
        {
            var entityNames = await _context.AppDefinitions
                .AsNoTracking()
                .OrderBy(entity => entity.Name)
                .Select(entity => new { entity.EntityCode, entity.Name })
                .ToListAsync(cancellationToken);

            var result = entityNames
                .GroupBy(entity => entity.EntityCode)
                .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

            if (!result.ContainsKey(BookingEntityCode))
            {
                result[BookingEntityCode] = "Бронирования";
            }

            return result;
        }

        private static TrashSelections ParseSelections(IEnumerable<string>? selectionKeys)
        {
            var result = new TrashSelections();
            if (selectionKeys == null)
            {
                return result;
            }

            foreach (var rawKey in selectionKeys)
            {
                if (string.IsNullOrWhiteSpace(rawKey))
                {
                    continue;
                }

                var parts = rawKey.Split(':', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id))
                {
                    continue;
                }

                if (string.Equals(parts[0], GenericObjectPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    result.GenericObjectIds.Add(id);
                }
                else if (string.Equals(parts[0], BookingPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    result.BookingIds.Add(id);
                }
            }

            return result;
        }

        private static string BuildSelectionKey(string prefix, Guid id) => $"{prefix}:{id}";

        private sealed class TrashSelections
        {
            public HashSet<Guid> GenericObjectIds { get; } = new();
            public HashSet<Guid> BookingIds { get; } = new();
        }
    }
}
