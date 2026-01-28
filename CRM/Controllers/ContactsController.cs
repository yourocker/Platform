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
        /// Используется, так как ModelBinder не умеет нативно биндить динамические ключи в Dictionary.
        /// </summary>
        private Dictionary<string, object> ExtractDynamicProps()
        {
            var dict = new Dictionary<string, object>();
            foreach (var key in Request.Form.Keys.Where(k => k.StartsWith("DynamicProps[")))
            {
                var systemName = key.Replace("DynamicProps[", "").Replace("]", "");
                var values = Request.Form[key].ToList();
                
                // Если значений несколько (checkbox array, multi-select) - сохраняем список, иначе строку
                if (values.Count > 1) dict[systemName] = values;
                else dict[systemName] = values.FirstOrDefault() ?? "";
            }
            return dict;
        }

        // --- ACTIONS ---

        public async Task<IActionResult> Index(string searchString, int? pageNumber, int? pageSize)
        {
            await LoadViewData();
            int actualPageSize = pageSize ?? 10;
            int actualPageNumber = pageNumber ?? 1;

            // 1. Сбор фильтров из URL
            var filters = Request.Query
                .Where(q => q.Key.StartsWith("f_") && !string.IsNullOrEmpty(q.Value))
                .ToDictionary(k => k.Key, v => v.Value.ToString());

            // 2. Поиск через Спецификацию (БД)
            var spec = new ContactSearchSpecification(searchString, filters, actualPageNumber, actualPageSize);
            
            var query = _context.Contacts.AsNoTracking();
            var filteredQuery = SpecificationEvaluator.GetQuery(query, spec);
            var contactsEntities = await filteredQuery.ToListAsync();

            // 3. Преобразование Entity -> DTO (Logic Decoupling)
            // Маппер также парсит JSONB поля в словарь для View
            var contactsDtos = contactsEntities.Select(ContactMapper.ToListDto).ToList();

            // 4. Подсчет количества для пагинации
            var countQuery = _context.Contacts.AsNoTracking().AsQueryable();
            foreach (var criterion in spec.Criteria) countQuery = countQuery.Where(criterion);
            int totalItems = await countQuery.CountAsync();

            ViewBag.TotalItems = totalItems;
            ViewBag.PageNumber = actualPageNumber;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)actualPageSize);
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentFilters = filters;

            // Отдаем DTO, как и ожидает теперь Index.cshtml
            return View(contactsDtos); 
        }

        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();
            
            var contact = await _context.Contacts
                .Include(c => c.Phones)
                .Include(c => c.Emails)
                .AsNoTracking() // Для чтения изменений не нужно
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (contact == null) return NotFound();
            
            await LoadViewData();
            
            // Преобразуем в DTO для деталей
            var dto = ContactMapper.ToDetailsDto(contact);
            return View(dto);
        }

        // GET: Create
        public async Task<IActionResult> Create()
        {
            await LoadViewData();
            // Отдаем пустую DTO, инициализированную дефолтными значениями
            return View(new ContactCreateDto());
        }

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContactCreateDto dto)
        {
            if (ModelState.IsValid)
            {
                // 1. Создаем заготовку сущности
                var contact = ContactMapper.ToEntity(dto);
                
                // 2. Извлекаем динамические данные напрямую из Request
                var dynamicProps = ExtractDynamicProps();

                // 3. Делегируем всю логику сохранения сервису
                await _contactService.CreateContactAsync(contact, dto.PhoneNumbers, dto.EmailAddresses, dynamicProps);
                
                return RedirectToAction(nameof(Index));
            }
            
            // Если ошибка валидации:
            await LoadViewData();
            // Восстанавливаем введенные динамические данные, чтобы пользователь не вводил их заново
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
            
            // Преобразуем существующую запись в DTO редактирования
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
                    // 1. Подготовка данных
                    var contact = new Contact(); 
                    ContactMapper.UpdateEntity(contact, dto); // Переносим базовые поля
                    
                    var dynamicProps = ExtractDynamicProps(); // Достаем JSON поля

                    // 2. Обновление через сервис
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
            
            // Ошибка валидации
            await LoadViewData();
            dto.DynamicValues = ExtractDynamicProps(); // Сохраняем введенное
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