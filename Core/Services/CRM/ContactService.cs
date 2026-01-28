using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Core.Data;
using Core.Entities.CRM;
using Core.Interfaces.CRM;
using Microsoft.EntityFrameworkCore;

namespace Core.Services.CRM
{
    public class ContactService : IContactService
    {
        private readonly AppDbContext _context;

        public ContactService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Contact> CreateContactAsync(Contact contact, IEnumerable<string> phones, IEnumerable<string> emails, Dictionary<string, object> dynamicProperties)
        {
            // 1. Базовая инициализация
            if (contact.Id == Guid.Empty) contact.Id = Guid.NewGuid();
            contact.EntityCode = "Contact";
            contact.CreatedAt = DateTime.UtcNow;

            // 2. Обработка динамических полей (JSON)
            if (dynamicProperties != null && dynamicProperties.Any())
            {
                contact.Properties = JsonSerializer.Serialize(dynamicProperties);
            }

            // 3. Расчет полного имени (Бизнес-логика сущности)
            contact.RecalculateFullName();
            contact.Name = contact.FullName;

            // 4. Обработка телефонов
            if (phones != null)
            {
                contact.Phones = phones
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => new ContactPhone { ContactId = contact.Id, Number = p.Trim() })
                    .ToList();
            }

            // 5. Обработка Email
            if (emails != null)
            {
                contact.Emails = emails
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => new ContactEmail { ContactId = contact.Id, Email = e.Trim() })
                    .ToList();
            }

            // 6. Сохранение
            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();

            return contact;
        }

        public async Task<Contact> UpdateContactAsync(Guid id, Contact source, IEnumerable<string> phones, IEnumerable<string> emails, Dictionary<string, object> dynamicProperties)
        {
            // 1. Загружаем оригинал с зависимостями
            var original = await _context.Contacts
                .Include(c => c.Phones)
                .Include(c => c.Emails)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (original == null) throw new KeyNotFoundException($"Contact with ID {id} not found.");

            // 2. Обновляем базовые поля
            original.FirstName = source.FirstName;
            original.LastName = source.LastName;
            original.MiddleName = source.MiddleName;
            
            // 3. Обновляем JSON свойства
            if (dynamicProperties != null && dynamicProperties.Any())
            {
                original.Properties = JsonSerializer.Serialize(dynamicProperties);
            }
            else
            {
                // Если пришел пустой словарь, но поля были - можно решить: затирать или нет.
                // В данном случае, если форма отправлена, считаем что это актуальное состояние.
                original.Properties = null; 
            }

            // 4. Пересчитываем FullName
            original.RecalculateFullName();
            original.Name = original.FullName;

            // 5. Обновляем телефоны (Стратегия: Удалить старые -> Добавить новые)
            // Это проще и надежнее, чем вычислять diff для простых списков
            _context.ContactPhones.RemoveRange(original.Phones);
            if (phones != null)
            {
                var newPhones = phones
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => new ContactPhone { ContactId = id, Number = p.Trim() })
                    .ToList();
                _context.ContactPhones.AddRange(newPhones);
            }

            // 6. Обновляем Email
            _context.ContactEmails.RemoveRange(original.Emails);
            if (emails != null)
            {
                var newEmails = emails
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => new ContactEmail { ContactId = id, Email = e.Trim() })
                    .ToList();
                _context.ContactEmails.AddRange(newEmails);
            }

            await _context.SaveChangesAsync();
            return original;
        }

        public async Task DeleteContactAsync(Guid id)
        {
            var contact = await _context.Contacts.FindAsync(id);
            if (contact != null)
            {
                _context.Contacts.Remove(contact);
                await _context.SaveChangesAsync();
            }
        }
    }
}