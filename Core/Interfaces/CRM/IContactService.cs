using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Entities.CRM;

namespace Core.Interfaces.CRM
{
    public interface IContactService
    {
        /// <summary>
        /// Создает новый контакт вместе с телефонами, почтами и динамическими полями.
        /// </summary>
        Task<Contact> CreateContactAsync(Contact contact, IEnumerable<string> phones, IEnumerable<string> emails, Dictionary<string, object> dynamicProperties);

        /// <summary>
        /// Обновляет существующий контакт. Полностью заменяет списки телефонов и почт.
        /// </summary>
        Task<Contact> UpdateContactAsync(Guid id, Contact source, IEnumerable<string> phones, IEnumerable<string> emails, Dictionary<string, object> dynamicProperties);

        /// <summary>
        /// Удаляет контакт.
        /// </summary>
        Task DeleteContactAsync(Guid id);
    }
}