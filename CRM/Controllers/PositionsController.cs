using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Data;
using Core.Entities.Company;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Core.Entities.Platform;
using CRM.Infrastructure;
using CRM.Infrastructure.Security;
using CRM.ViewModels.Filters;

namespace CRM.Controllers
{
    [TenantAuthorize(TenantPermissions.ManageCompanyStructure)]
    public class PositionsController : BasePlatformController
    {
        public PositionsController(AppDbContext context, IWebHostEnvironment hostingEnvironment) 
            : base(context, hostingEnvironment)
        {
        }

        private FilterPanelViewModel BuildFilterPanelModel(
            IReadOnlyCollection<AppFieldDefinition> dynamicFields,
            IDictionary<string, string> currentFilters,
            int pageSize)
        {
            var lookupData = ViewBag.LookupData as Dictionary<string, List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>>
                             ?? new Dictionary<string, List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>>();

            var fields = new List<FilterFieldViewModel>
            {
                new() { Key = "f_Name", Label = "Название", Kind = FilterInputKind.Text, Value = TryGetFilterValue(currentFilters, "f_Name") }
            };

            fields.AddRange(BuildDynamicFilterFields(dynamicFields, lookupData, currentFilters));

            return new FilterPanelViewModel
            {
                ActionUrl = Url.Action(nameof(Index)) ?? "/Positions",
                ResetUrl = Url.Action(nameof(Index)) ?? "/Positions",
                EntityCode = "Position",
                ViewCode = "Index",
                SearchValue = ViewBag.CurrentSearch as string ?? string.Empty,
                SearchPlaceholder = "Быстрый поиск",
                PageSize = pageSize,
                ExpandedByDefault = currentFilters.Any(),
                Fields = fields
            };
        }

        public async Task<IActionResult> Index(string? searchString, int? pageNumber, int? pageSize, Dictionary<string, string> filters)
        {
            await LoadDynamicFields("Position");
            var dynamicFields = ViewBag.DynamicFields as List<AppFieldDefinition> ?? new List<AppFieldDefinition>();
            var dynamicFieldMap = dynamicFields.ToDictionary(field => field.SystemName, field => field, StringComparer.OrdinalIgnoreCase);
            var query = _context.Positions.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
                query = query.Where(p => EF.Functions.ILike(p.Name, $"%{searchString}%"));

            if (filters != null && filters.Any())
            {
                foreach (var filter in filters)
                {
                    if (string.IsNullOrWhiteSpace(filter.Value)) continue;
                    if (filter.Key == "f_Name")
                        query = query.Where(p => EF.Functions.ILike(p.Name, $"%{filter.Value}%"));
                    else if (filter.Key.StartsWith("f_dyn_"))
                    {
                        var fieldName = filter.Key.Replace("f_dyn_", "");
                        if (dynamicFieldMap.TryGetValue(fieldName, out var field))
                        {
                            query = query.ApplyDynamicPropertyFilter(nameof(Position.Properties), field, filter.Value);
                        }
                    }
                }
            }

            int actualPageSize = pageSize ?? 10;
            int actualPageNumber = pageNumber ?? 1;
            int totalItems = await query.CountAsync();
            
            var positions = await query
                .OrderBy(p => p.Name)
                .Skip((actualPageNumber - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.TotalItems = totalItems;
            ViewBag.PageNumber = actualPageNumber;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)actualPageSize);
            var currentFilters = filters ?? new Dictionary<string, string>();
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentFilters = currentFilters;
            ViewBag.FilterPanelModel = BuildFilterPanelModel(dynamicFields, currentFilters, actualPageSize);

            return View(positions);
        }

        public async Task<IActionResult> Create(bool modal = false)
        {
            await LoadDynamicFields("Position");
            ViewBag.IsModal = modal;
            return View();
        }

        [HttpPost] [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Position position, IFormCollection form, bool modal = false)
        {
            position.Id = Guid.NewGuid();
            await SaveDynamicProperties(position, form, "Position");
            if (ModelState.IsValid)
            {
                _context.Add(position);
                await _context.SaveChangesAsync();
                FinalizeDynamicFilePaths(position, "Position", position.Id.ToString());
                await _context.SaveChangesAsync();

                if (modal)
                {
                    return BuildModalCreatedContentResult("Position", position.Id, position.Name);
                }

                return RedirectToAction(nameof(Index));
            }
            await LoadDynamicFields("Position");
            ViewBag.IsModal = modal;
            return View(position);
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var position = await _context.Positions.FindAsync(id);
            if (position == null) return NotFound();
            await LoadDynamicFields("Position");
            return View(position);
        }

        [HttpPost] [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Position position, IFormCollection form, bool modal = false)
        {
            if (id != position.Id) return NotFound();
            await SaveDynamicProperties(position, form, "Position");
            if (ModelState.IsValid)
            {
                try {
                    FinalizeDynamicFilePaths(position, "Position", position.Id.ToString());
                    _context.Update(position);
                    await _context.SaveChangesAsync();
                } catch (DbUpdateConcurrencyException) {
                    if (!PositionExists(position.Id)) return NotFound(); else throw;
                }
                if (modal)
                {
                    return BuildModalUpdatedContentResult("Position", position.Id, position.Name);
                }

                return RedirectToAction(nameof(Index));
            }
            await LoadDynamicFields("Position");
            return View(position);
        }

        // --- GET: ПОДТВЕРЖДЕНИЕ УДАЛЕНИЯ ---
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();
            var position = await _context.Positions.FirstOrDefaultAsync(m => m.Id == id);
            if (position == null) return NotFound();
            
            ViewBag.HasEmployees = await _context.StaffAppointments.AnyAsync(a => a.PositionId == id);
            return View(position);
        }

        // --- POST: САМО УДАЛЕНИЕ ---
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var position = await _context.Positions.FindAsync(id);
            if (position != null)
            {
                var hasEmployees = await _context.StaffAppointments.AnyAsync(a => a.PositionId == id);
                if (!hasEmployees) 
                {
                    _context.Positions.Remove(position);
                    await _context.SaveChangesAsync();
                }
            }
            return RedirectToAction(nameof(Index));
        }

        private bool PositionExists(Guid id) => _context.Positions.Any(e => e.Id == id);
    }
}
