using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Data;
using Core.Services.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

namespace CRM.Controllers
{
    public class DataController : BasePlatformController
    {
        public DataController(AppDbContext context, IWebHostEnvironment hostingEnvironment) 
            : base(context, hostingEnvironment)
        {
        }

        [Route("Data/{entityCode}")]
        public async Task<IActionResult> Index(string entityCode)
        {
            var definition = await _context.AppDefinitions
                .Include(a => a.Fields)
                .FirstOrDefaultAsync(a => a.EntityCode == entityCode);

            if (definition == null) return NotFound();

            var objects = await _context.GenericObjects
                .Where(o => o.EntityCode == entityCode)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            ViewBag.Definition = definition;
            ViewBag.EntityCode = entityCode;
            ViewBag.DynamicFields = definition.Fields.OrderBy(f => f.SortOrder).ToList();
            ViewBag.NamesMap = new Dictionary<System.Guid, string>();
            ViewBag.TotalItems = objects.Count;
            ViewBag.PageNumber = 1;
            ViewBag.PageSize = 10;
            ViewBag.TotalPages = 1;
            ViewBag.CurrentFilters = new Dictionary<string, string>();
            
            return View("~/Views/GenericObjects/Index.cshtml", objects.Select(GenericObjectMapper.ToListDto).ToList());
        }
    }
}
