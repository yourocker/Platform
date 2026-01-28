using Core.DTOs.CRM;
using Core.Entities.CRM;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Core.Services.CRM
{
    public static class ContactMapper
    {
        // Вспомогательный метод для парсинга JSON
        private static Dictionary<string, object> ParseProperties(string? json)
        {
            if (string.IsNullOrEmpty(json)) return new Dictionary<string, object>();
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(json) 
                       ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        public static ContactListDto ToListDto(Contact contact)
        {
            return new ContactListDto
            {
                Id = contact.Id,
                FullName = contact.FullName,
                LastName = contact.LastName,
                FirstName = contact.FirstName,
                MiddleName = contact.MiddleName,
                PhoneNumbers = contact.Phones?.Select(p => p.Number).ToList() ?? new(),
                EmailAddresses = contact.Emails?.Select(e => e.Email).ToList() ?? new(),
                DynamicValues = ParseProperties(contact.Properties) // <--- Парсим JSON
            };
        }

        public static ContactDetailsDto ToDetailsDto(Contact contact)
        {
            return new ContactDetailsDto
            {
                Id = contact.Id,
                LastName = contact.LastName,
                FirstName = contact.FirstName,
                MiddleName = contact.MiddleName,
                FullName = contact.FullName,
                PhoneNumbers = contact.Phones?.Select(p => p.Number).ToList() ?? new(),
                EmailAddresses = contact.Emails?.Select(e => e.Email).ToList() ?? new(),
                DynamicValues = ParseProperties(contact.Properties) // <--- Парсим JSON
            };
        }

        public static ContactEditDto ToEditDto(Contact contact)
        {
            return new ContactEditDto
            {
                Id = contact.Id,
                LastName = contact.LastName,
                FirstName = contact.FirstName,
                MiddleName = contact.MiddleName,
                PhoneNumbers = contact.Phones?.Select(p => p.Number).ToList() ?? new(),
                EmailAddresses = contact.Emails?.Select(e => e.Email).ToList() ?? new(),
                DynamicValues = ParseProperties(contact.Properties) // <--- Парсим JSON
            };
        }

        public static Contact ToEntity(ContactCreateDto dto)
        {
            return new Contact
            {
                LastName = dto.LastName,
                FirstName = dto.FirstName,
                MiddleName = dto.MiddleName
            };
        }

        public static void UpdateEntity(Contact contact, ContactEditDto dto)
        {
            contact.LastName = dto.LastName;
            contact.FirstName = dto.FirstName;
            contact.MiddleName = dto.MiddleName;
        }
    }
}