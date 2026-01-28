using Core.Entities.CRM;
using Microsoft.EntityFrameworkCore;
using Core.Data.Extensions;
using Core.Specifications;

namespace Core.Specifications.CRM
{
    public class ContactSearchSpecification : BaseSpecification<Contact>
    {
        public ContactSearchSpecification(string? searchString, Dictionary<string, string> filters, int pageNumber, int pageSize)
        {
            AddInclude(c => c.Phones);
            AddInclude(c => c.Emails);

            // Глобальный поиск
            if (!string.IsNullOrEmpty(searchString))
            {
                var s = searchString.Trim();
                AddCriteria(c => 
                    EF.Functions.ILike(c.FullName, $"%{s}%") ||
                    c.Phones.Any(p => EF.Functions.ILike(p.Number, $"%{s}%")) ||
                    c.Emails.Any(e => EF.Functions.ILike(e.Email, $"%{s}%"))
                );
            }

            // Детальные фильтры
            foreach (var filter in filters)
            {
                var key = filter.Key.StartsWith("f_") ? filter.Key.Substring(2) : filter.Key;
                var val = filter.Value.Trim();

                if (string.IsNullOrEmpty(val)) continue;

                switch (key)
                {
                    case "LastName":
                        AddCriteria(c => EF.Functions.ILike(c.LastName, $"%{val}%"));
                        break;
                    case "FirstName":
                        AddCriteria(c => EF.Functions.ILike(c.FirstName, $"%{val}%"));
                        break;
                    case "MiddleName":
                        AddCriteria(c => EF.Functions.ILike(c.MiddleName, $"%{val}%"));
                        break;
                    case "Phone": // <--- ДОБАВЛЕНО
                        AddCriteria(c => c.Phones.Any(p => EF.Functions.ILike(p.Number, $"%{val}%")));
                        break;
                    case "Email": // <--- ДОБАВЛЕНО
                        AddCriteria(c => c.Emails.Any(e => EF.Functions.ILike(e.Email, $"%{val}%")));
                        break;
                    default:
                        // Поиск по JSON
                        AddCriteria(c => c.Properties != null && 
                            EF.Functions.ILike(NpgsqlJsonExtensions.JsonExtractPathText(c.Properties, key), $"%{val}%"));
                        break;
                }
            }

            ApplyOrderBy(c => c.FullName);
            ApplyPaging((pageNumber - 1) * pageSize, pageSize);
        }
    }
}