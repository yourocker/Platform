using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core.Data;
using Core.Entities.CRM;
using Core.Entities.Platform;
using System.Text.Json;

namespace CRM.Controllers
{
    public class ContactsController : Controller
    {
        private readonly AppDbContext _context;

        public ContactsController(AppDbContext context)
        {
            _context = context;
        }

        private async Task LoadViewData()
        {
            var appDef = await _context.AppDefinitions
                .Include(a => a.Fields)
                .FirstOrDefaultAsync(a => a.EntityCode == "Contact");

            if (appDef != null)
            {
                ViewBag.DynamicFields = appDef.Fields.OrderBy(f => f.SortOrder).ToList();
            }
        }

        public async Task<IActionResult> Index(string searchString, int? pageNumber, int? pageSize)
        {
            await LoadViewData();

            // 1. Инициализация параметров пагинации
            int actualPageSize = pageSize ?? 10;
            int actualPageNumber = pageNumber ?? 1;
            
            var query = _context.Contacts
                .Include(c => c.Phones)
                .Include(c => c.Emails)
                .AsQueryable();

            // 2. ГЛОБАЛЬНЫЙ ПОИСК
            if (!string.IsNullOrEmpty(searchString))
            {
                var s = searchString.Trim();
                // (string)(object)c.Properties — это критически важный каст для корректного SQL Properties::text
                query = query.Where(c => 
                    EF.Functions.ILike(c.FullName, $"%{s}%") ||
                    c.Phones.Any(p => EF.Functions.ILike(p.Number, $"%{s}%")) ||
                    c.Emails.Any(e => EF.Functions.ILike(e.Email, $"%{s}%")) ||
                    (c.Properties != null && EF.Functions.ILike((string)(object)c.Properties, $"%{s}%"))
                );
            }

            // 3. ДЕТАЛЬНАЯ ФИЛЬТРАЦИЯ
            var filters = Request.Query.Where(q => q.Key.StartsWith("f_") && !string.IsNullOrEmpty(q.Value));
            foreach (var filter in filters)
            {
                var fieldName = filter.Key.Substring(2);
                var val = filter.Value.ToString().Trim();

                query = fieldName switch
                {
                    "LastName" => query.Where(c => EF.Functions.ILike(c.LastName, $"%{val}%")),
                    "FirstName" => query.Where(c => EF.Functions.ILike(c.FirstName, $"%{val}%")),
                    "MiddleName" => query.Where(c => EF.Functions.ILike(c.MiddleName, $"%{val}%")),
                    "FullName" => query.Where(c => EF.Functions.ILike(c.FullName, $"%{val}%")),
                    // Для кастомных полей: поиск подстроки "Ключ":"Значение" внутри JSON-текста
                    _ => query.Where(c => c.Properties != null && EF.Functions.ILike((string)(object)c.Properties, $"%\"{fieldName}\":%\"{val}\"%"))
                };
            }

            // 4. ПОДСЧЕТ И ПОЛУЧЕНИЕ ДАННЫХ
            int totalItems = await query.CountAsync();
            
            var contacts = await query
                .OrderBy(c => c.FullName)
                .Skip((actualPageNumber - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            // Передача метаданных во View
            ViewBag.TotalItems = totalItems;
            ViewBag.PageNumber = actualPageNumber;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)actualPageSize);
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentFilters = filters.ToDictionary(x => x.Key, x => x.Value.ToString());

            return View(contacts);
        }

        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();
            var contact = await _context.Contacts.Include(c => c.Phones).Include(c => c.Emails).FirstOrDefaultAsync(m => m.Id == id);
            if (contact == null) return NotFound();
            await LoadViewData();
            return View(contact);
        }

        public async Task<IActionResult> Create()
        {
            var contact = new Contact { Id = Guid.NewGuid(), EntityCode = "Contact" };
            await LoadViewData();
            return View(contact);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Contact contact, string[] PhoneNumbers, string[] EmailAddresses)
        {
            var dynamicData = ExtractDynamicProps();
            if (dynamicData.Any()) contact.Properties = JsonSerializer.Serialize(dynamicData);

            contact.CreatedAt = DateTime.UtcNow;
            contact.RecalculateFullName();
            contact.Name = contact.FullName;

            if (PhoneNumbers != null)
                contact.Phones = PhoneNumbers.Where(p => !string.IsNullOrEmpty(p)).Select(p => new ContactPhone { ContactId = contact.Id, Number = p }).ToList();

            if (EmailAddresses != null)
                contact.Emails = EmailAddresses.Where(e => !string.IsNullOrEmpty(e)).Select(e => new ContactEmail { ContactId = contact.Id, Email = e }).ToList();

            ModelState.Remove(nameof(contact.EntityCode));
            ModelState.Remove(nameof(contact.Name));
            ModelState.Remove(nameof(contact.FullName));

            if (ModelState.IsValid)
            {
                _context.Add(contact);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await LoadViewData();
            return View(contact);
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var contact = await _context.Contacts.Include(c => c.Phones).Include(c => c.Emails).FirstOrDefaultAsync(x => x.Id == id);
            if (contact == null) return NotFound();
            await LoadViewData();
            return View(contact);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Contact contact, string[] PhoneNumbers, string[] EmailAddresses)
        {
            if (id != contact.Id) return NotFound();

            var dynamicData = ExtractDynamicProps();
            contact.Properties = dynamicData.Any() ? JsonSerializer.Serialize(dynamicData) : null;

            ModelState.Remove(nameof(contact.EntityCode));
            ModelState.Remove(nameof(contact.Name));
            ModelState.Remove(nameof(contact.FullName));

            if (ModelState.IsValid)
            {
                try
                {
                    var original = await _context.Contacts.Include(c => c.Phones).Include(c => c.Emails).FirstOrDefaultAsync(c => c.Id == id);
                    if (original == null) return NotFound();

                    original.LastName = contact.LastName;
                    original.FirstName = contact.FirstName;
                    original.MiddleName = contact.MiddleName;
                    original.Properties = contact.Properties;
                    original.RecalculateFullName();
                    original.Name = original.FullName;

                    _context.ContactPhones.RemoveRange(original.Phones);
                    if (PhoneNumbers != null)
                        original.Phones = PhoneNumbers.Where(p => !string.IsNullOrEmpty(p)).Select(p => new ContactPhone { ContactId = id, Number = p }).ToList();

                    _context.ContactEmails.RemoveRange(original.Emails);
                    if (EmailAddresses != null)
                        original.Emails = EmailAddresses.Where(e => !string.IsNullOrEmpty(e)).Select(e => new ContactEmail { ContactId = id, Email = e }).ToList();

                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException) { if (!ContactExists(contact.Id)) return NotFound(); throw; }
            }
            await LoadViewData();
            return View(contact);
        }

        private Dictionary<string, object> ExtractDynamicProps()
        {
            var dict = new Dictionary<string, object>();
            foreach (var key in Request.Form.Keys.Where(k => k.StartsWith("DynamicProps[")))
            {
                var systemName = key.Replace("DynamicProps[", "").Replace("]", "");
                var values = Request.Form[key].ToList();
                if (values.Count > 1) dict[systemName] = values;
                else dict[systemName] = values.FirstOrDefault() ?? "";
            }
            return dict;
        }

        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();
            var contact = await _context.Contacts.FirstOrDefaultAsync(m => m.Id == id);
            return contact == null ? NotFound() : View(contact);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var contact = await _context.Contacts.FindAsync(id);
            if (contact != null) { _context.Contacts.Remove(contact); await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }

        private bool ContactExists(Guid id) => _context.Contacts.Any(e => e.Id == id);
    }
}