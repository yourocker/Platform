using Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Controllers
{
    public class DataController : BasePlatformController
    {
        public DataController(AppDbContext context, IWebHostEnvironment hostingEnvironment) 
            : base(context, hostingEnvironment)
        {
        }

        [Route("Data/{entityCode}")]
        public IActionResult Index(string entityCode)
        {
            var dedicatedRedirect = entityCode.ToLowerInvariant() switch
            {
                "contact" => RedirectToAction("Index", "Contacts"),
                "lead" => RedirectToAction("Index", "Leads"),
                "deal" => RedirectToAction("Index", "Deals"),
                _ => null
            };

            if (dedicatedRedirect != null)
            {
                return dedicatedRedirect;
            }

            return RedirectToAction("Index", "GenericObjects", new { entityCode });
        }
    }
}
