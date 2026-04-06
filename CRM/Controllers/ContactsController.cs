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
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using Core.Interfaces.Platform;
using CRM.Infrastructure;
using CRM.ViewModels.Filters;

namespace CRM.Controllers
{
    public class ContactsController : BasePlatformController
    {
        private readonly IContactService _contactService;
        private readonly IEntityTimelineService _timelineService;

        public ContactsController(
            AppDbContext context,
            IContactService contactService,
            IWebHostEnvironment hostingEnvironment,
            IEntityTimelineService timelineService)
            : base(context, hostingEnvironment)
        {
            _contactService = contactService;
            _timelineService = timelineService;
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
                var fields = appDef.Fields.OrderBy(f => f.SortOrder).ToList();
                ViewBag.DynamicFields = fields;
                ViewBag.LookupData = await BuildEntityLinkLookupDataAsync(fields);
            }
        }

        private FilterPanelViewModel BuildFilterPanelModel(
            Dictionary<string, string> currentFilters,
            IReadOnlyCollection<AppFieldDefinition> dynamicFields)
        {
            var lookupData = ViewBag.LookupData as Dictionary<string, List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>>
                             ?? new Dictionary<string, List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>>();

            var fields = new List<FilterFieldViewModel>
            {
                new() { Key = "f_LastName", Label = "Фамилия", Kind = FilterInputKind.Text, Value = TryGetFilterValue(currentFilters, "f_LastName") },
                new() { Key = "f_FirstName", Label = "Имя", Kind = FilterInputKind.Text, Value = TryGetFilterValue(currentFilters, "f_FirstName") },
                new() { Key = "f_MiddleName", Label = "Отчество", Kind = FilterInputKind.Text, Value = TryGetFilterValue(currentFilters, "f_MiddleName") },
                new() { Key = "f_Phone", Label = "Телефон", Kind = FilterInputKind.Text, Value = TryGetFilterValue(currentFilters, "f_Phone") },
                new() { Key = "f_Email", Label = "Email", Kind = FilterInputKind.Text, Value = TryGetFilterValue(currentFilters, "f_Email") }
            };

            fields.AddRange(BuildDynamicFilterFields(dynamicFields, lookupData, currentFilters));

            return new FilterPanelViewModel
            {
                ActionUrl = Url.Action(nameof(Index)) ?? "/Contacts",
                ResetUrl = Url.Action(nameof(Index)) ?? "/Contacts",
                EntityCode = "Contact",
                ViewCode = "Index",
                SearchValue = ViewBag.CurrentSearch as string ?? string.Empty,
                SearchPlaceholder = "Быстрый поиск",
                PageSize = ViewBag.PageSize is int pageSize ? pageSize : 10,
                ExpandedByDefault = currentFilters.Any(),
                Fields = fields
            };
        }

        private static Dictionary<string, object> DeserializeDynamicProps(string? properties)
        {
            if (string.IsNullOrWhiteSpace(properties))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(properties) ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        // --- ACTIONS ---

        // GET: Contacts
        public async Task<IActionResult> Index(
            string? searchString, 
            int pageNumber = 1, 
            int? pageSize = null,
            string? sortOrder = null,
            [FromQuery] Dictionary<string, string>? filters = null)
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
            filters ??= new Dictionary<string, string>();

            // 4. Получение данных
            
            // 4.1. Подсчет общего количества (БЕЗ сортировки и пагинации)
            var dynamicFieldMap = dynamicFields.ToDictionary(field => field.SystemName, field => field, StringComparer.OrdinalIgnoreCase);

            var countSpec = new ContactSearchSpecification(searchString, filters, dynamicFieldMap);
            var totalItems = await SpecificationEvaluator.GetQuery<Contact>(_context.Contacts.AsQueryable(), countSpec).CountAsync();

            // 4.2. Получение страницы данных (С СОРТИРОВКОЙ и пагинацией)
            // Передаем sortOrder в спецификацию
            var listSpec = new ContactSearchSpecification(searchString, filters, dynamicFieldMap, pageNumber, actualPageSize, sortOrder);
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
            var currentFilters = filters ?? new Dictionary<string, string>();
            ViewBag.CurrentFilters = currentFilters;
            ViewBag.FilterPanelModel = BuildFilterPanelModel(currentFilters, dynamicFields);

            return View(contactDtos);
        }

        public async Task<IActionResult> Details(Guid? id, bool modal = false)
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
            ViewBag.TimelineEvents = await _timelineService.GetEventsAsync(contact.Id, "Contact");
            ViewBag.IsModal = modal;
            return View(dto);
        }

        // GET: Create
        public async Task<IActionResult> Create(bool modal = false)
        {
            await LoadViewData();
            ViewBag.IsModal = modal;
            return View(new ContactCreateDto());
        }

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContactCreateDto dto, bool modal = false)
        {
            var contact = ContactMapper.ToEntity(dto);
            contact.Id = Guid.NewGuid();
            await SaveDynamicProperties(contact, Request.Form, "Contact");

            if (ModelState.IsValid)
            {
                var createdContact = await _contactService.CreateContactAsync(contact, dto.PhoneNumbers, dto.EmailAddresses);

                await _timelineService.LogEventAsync(
                    createdContact.Id,
                    "Contact",
                    CrmEventType.System,
                    "Контакт создан",
                    $"Создан контакт \"{createdContact.FullName}\".",
                    TryGetCurrentEmployeeId());

                if (modal)
                {
                    return BuildModalCreatedContentResult("Contact", createdContact.Id, createdContact.FullName);
                }

                return RedirectToAction(nameof(Index));
            }
            await LoadViewData();
            ViewBag.IsModal = modal;
            dto.DynamicValues = DeserializeDynamicProps(contact.Properties);
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
        public async Task<IActionResult> Edit(Guid id, ContactEditDto dto, bool modal = false)
        {
            if (id != dto.Id) return NotFound();

            var beforeContact = await _context.Contacts
                .Include(c => c.Phones)
                .Include(c => c.Emails)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);
            if (beforeContact == null)
            {
                return NotFound();
            }

            var contact = new Contact
            {
                Id = id,
                Properties = beforeContact.Properties
            };
            ContactMapper.UpdateEntity(contact, dto);
            await SaveDynamicProperties(contact, Request.Form, "Contact");

            if (ModelState.IsValid)
            {
                try
                {
                    await _contactService.UpdateContactAsync(id, contact, dto.PhoneNumbers, dto.EmailAddresses);

                    var fieldLabels = await LoadFieldLabelMapAsync("Contact");
                    var summary = BuildContactChangeSummary(beforeContact, dto, DeserializeDynamicProps(contact.Properties), fieldLabels);

                    await _timelineService.LogEventAsync(
                        id,
                        "Contact",
                        CrmEventType.FieldChange,
                        "Контакт обновлён",
                        summary,
                        TryGetCurrentEmployeeId());
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
                if (modal)
                {
                    return BuildModalUpdatedContentResult("Contact", id, $"{dto.LastName} {dto.FirstName} {dto.MiddleName}".Trim());
                }

                return RedirectToAction(nameof(Index));
            }
            
            await LoadViewData();
            dto.DynamicValues = DeserializeDynamicProps(contact.Properties); 
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
            var contact = await _context.Contacts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            await _contactService.DeleteContactAsync(id);

            if (contact != null)
            {
                await _timelineService.LogEventAsync(
                    id,
                    "Contact",
                    CrmEventType.System,
                    "Контакт перемещён в корзину",
                    $"Контакт \"{contact.FullName}\" перемещён в корзину.",
                    TryGetCurrentEmployeeId());
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ContactExists(Guid id) => _context.Contacts.Any(e => e.Id == id);

        private static string? BuildContactChangeSummary(
            Contact beforeContact,
            ContactEditDto dto,
            Dictionary<string, object> dynamicProps,
            IReadOnlyDictionary<string, string> fieldLabels)
        {
            var changes = new List<string>();

            TimelineChangeFormatter.AddScalarChange(changes, "Фамилия", beforeContact.LastName, dto.LastName);
            TimelineChangeFormatter.AddScalarChange(changes, "Имя", beforeContact.FirstName, dto.FirstName);
            TimelineChangeFormatter.AddScalarChange(changes, "Отчество", beforeContact.MiddleName, dto.MiddleName);
            TimelineChangeFormatter.AddCollectionChange(changes, "Телефоны", beforeContact.Phones.Select(p => p.Number), dto.PhoneNumbers);
            TimelineChangeFormatter.AddCollectionChange(changes, "Email", beforeContact.Emails.Select(e => e.Email), dto.EmailAddresses);

            var beforeProps = TimelineChangeFormatter.ParseDynamicProperties(beforeContact.Properties);
            var afterProps = TimelineChangeFormatter.ParseDynamicProperties(
                dynamicProps != null && dynamicProps.Any()
                    ? JsonSerializer.Serialize(dynamicProps)
                    : null);

            TimelineChangeFormatter.AddDictionaryChanges(
                changes,
                beforeProps,
                afterProps,
                key => fieldLabels.TryGetValue(key, out var label) ? label : key);

            return TimelineChangeFormatter.BuildSummary(changes);
        }
    }
}
