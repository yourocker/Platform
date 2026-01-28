using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core.Data;
using Core.Entities.CRM;
using Core.Entities.Platform;
using Core.Specifications;
using Core.Specifications.CRM;
using Core.Interfaces.CRM;
using Core.DTOs.CRM;
using Core.Services.CRM;
using Core.UI.Grid;

namespace CRM.Controllers
{
    public class ContactsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IContactService _contactService;

        public ContactsController(AppDbContext context, IContactService contactService)
        {
            _context = context;
            _contactService = contactService;
        }

        // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---

        private async Task LoadViewData()
        {
            var appDef = await _context.AppDefinitions
                .Include(a => a.Fields)
                .FirstOrDefaultAsync(a => a.EntityCode == "Contact");

            if (appDef != null)
            {
                // Сортируем поля для корректного отображения в форме
                ViewBag.DynamicFields = appDef.Fields.OrderBy(f => f.SortOrder).ToList();
            }
        }

        /// <summary>
        /// Извлекает динамические поля из формы (Request.Form) в словарь.
        /// </summary>
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

        // --- ACTIONS ---

        // GET: Contacts
        public async Task<IActionResult> Index(
            string searchString, 
            int pageNumber = 1, 
            int? pageSize = null, // <--- Изменили на nullable int, чтобы различать отсутствие параметра
            string? sortOrder = null, // <--- НОВЫЙ ПАРАМЕТР СОРТИРОВКИ
            string? f_LastName = null,
            string? f_FirstName = null,
            string? f_MiddleName = null)
        {
            // 1. ЛОГИКА PAGE SIZE (COOKIE)
            // Если pageSize пришел в запросе - сохраняем в куки и используем.
            // Если не пришел - пытаемся достать из куки.
            // Если и там нет - используем дефолт 10.
            if (pageSize.HasValue)
            {
                Response.Cookies.Append("crm_pagesize", pageSize.Value.ToString(), new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
            }
            else
            {
                if (Request.Cookies.TryGetValue("crm_pagesize", out string cookieVal) && int.TryParse(cookieVal, out int parsedVal))
                {
                    pageSize = parsedVal;
                }
                else
                {
                    pageSize = 10;
                }
            }
            
            // Гарантируем, что pageSize имеет значение (для дальнейшего кода)
            int actualPageSize = pageSize.Value;

            // 2. Подгружаем поля
            await LoadViewData(); 
            var dynamicFields = ViewBag.DynamicFields as List<AppFieldDefinition> ?? new List<AppFieldDefinition>();

            // 3. Сбор фильтров
            var filters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(f_LastName)) filters.Add("LastName", f_LastName);
            if (!string.IsNullOrEmpty(f_FirstName)) filters.Add("FirstName", f_FirstName);
            if (!string.IsNullOrEmpty(f_MiddleName)) filters.Add("MiddleName", f_MiddleName);
            
            // Динамические фильтры из Query String
            foreach (var key in Request.Query.Keys)
            {
                if (key.StartsWith("f_") && !new[] { "f_LastName", "f_FirstName", "f_MiddleName" }.Contains(key))
                {
                    var val = Request.Query[key].ToString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        var fieldName = key.Substring(2);
                        filters.Add(fieldName, val);
                    }
                }
            }

            // 4. Получение данных
            
            // 4.1. Подсчет общего количества (БЕЗ сортировки и пагинации)
            var countSpec = new ContactSearchSpecification(searchString, filters);
            var totalItems = await SpecificationEvaluator.GetQuery<Contact>(_context.Contacts.AsQueryable(), countSpec).CountAsync();

            // 4.2. Получение страницы данных (С СОРТИРОВКОЙ и пагинацией)
            // Передаем sortOrder в спецификацию
            var listSpec = new ContactSearchSpecification(searchString, filters, pageNumber, actualPageSize, sortOrder);
            var contacts = await SpecificationEvaluator.GetQuery<Contact>(_context.Contacts.AsQueryable(), listSpec).ToListAsync();

            // Маппинг в DTO
            var contactDtos = contacts.Select(c => ContactMapper.ToListDto(c)).ToList();

            // 5. --- КОНФИГУРАЦИЯ ГРИДА ---
            var gridConfig = new GridConfig<ContactListDto>()
                // Добавляем sortKey для сложных полей, логику которых мы прописали в Спецификации
                .AddColumn(x => x.FullName, "ФИО", "col-fullname", GridColumnType.LinkBold, sortKey: "fullname") 
                .AddColumn(x => x.LastName, "Фамилия", "col-lastname", visible: false, sortKey: "lastname") 
                .AddColumn(x => x.FirstName, "Имя", "col-firstname", visible: false, sortKey: "firstname")   
                .AddColumn(x => x.MiddleName, "Отчество", "col-middlename", visible: false, sortKey: "middlename") 
                .AddColumn(x => x.PhoneNumbers, "Телефон", "col-phone", GridColumnType.PhoneList, sortKey: "phone") // Сортировка по 1-му номеру
                .AddColumn(x => x.EmailAddresses, "Email", "col-email", GridColumnType.EmailList, sortKey: "email") // Сортировка по 1-му email
                .AddDynamicFields(dynamicFields) // Сортировка включится автоматически по ключам JSON
                .AddActions();

            // Передача данных во View
            ViewBag.GridConfig = gridConfig;
            ViewBag.CurrentSearch = searchString;
            ViewBag.PageNumber = pageNumber;
            ViewBag.PageSize = actualPageSize; // Передаем актуальный размер
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)actualPageSize);
            
            // Возвращаем фильтры для восстановления в UI
            ViewBag.CurrentFilters = filters.ToDictionary(k => "f_" + k.Key, v => v.Value);

            return View(contactDtos);
        }

        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();
            
            var contact = await _context.Contacts
                .Include(c => c.Phones)
                .Include(c => c.Emails)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (contact == null) return NotFound();
            
            await LoadViewData();
            
            var dto = ContactMapper.ToDetailsDto(contact);
            return View(dto);
        }

        // GET: Create
        public async Task<IActionResult> Create()
        {
            await LoadViewData();
            return View(new ContactCreateDto());
        }

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContactCreateDto dto)
        {
            if (ModelState.IsValid)
            {
                var contact = ContactMapper.ToEntity(dto);
                var dynamicProps = ExtractDynamicProps();
                await _contactService.CreateContactAsync(contact, dto.PhoneNumbers, dto.EmailAddresses, dynamicProps);
                return RedirectToAction(nameof(Index));
            }
            await LoadViewData();
            dto.DynamicValues = ExtractDynamicProps();
            return View(dto);
        }

        // GET: Edit
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            
            var contact = await _context.Contacts
                .Include(c => c.Phones)
                .Include(c => c.Emails)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            
            if (contact == null) return NotFound();
            
            await LoadViewData();
            var dto = ContactMapper.ToEditDto(contact);
            return View(dto);
        }

        // POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, ContactEditDto dto)
        {
            if (id != dto.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var contact = new Contact(); 
                    ContactMapper.UpdateEntity(contact, dto); 
                    var dynamicProps = ExtractDynamicProps(); 
                    await _contactService.UpdateContactAsync(id, contact, dto.PhoneNumbers, dto.EmailAddresses, dynamicProps);
                }
                catch (KeyNotFoundException)
                {
                    return NotFound();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ContactExists(dto.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            
            await LoadViewData();
            dto.DynamicValues = ExtractDynamicProps(); 
            return View(dto);
        }

        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();
            var contact = await _context.Contacts.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (contact == null) return NotFound();
            var dto = ContactMapper.ToDetailsDto(contact);
            return View(dto); 
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            await _contactService.DeleteContactAsync(id);
            return RedirectToAction(nameof(Index));
        }

        private bool ContactExists(Guid id) => _context.Contacts.Any(e => e.Id == id);
    }
}