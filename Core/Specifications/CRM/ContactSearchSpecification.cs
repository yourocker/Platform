using System.Linq;
using Core.Entities.CRM;
using Microsoft.EntityFrameworkCore;
using Core.Data.Extensions;
using Core.Specifications;

namespace Core.Specifications.CRM
{
    public class ContactSearchSpecification : BaseSpecification<Contact>
    {
        /// <summary>
        /// Конструктор для ПОДСЧЕТА количества (без пагинации и сортировки)
        /// </summary>
        public ContactSearchSpecification(string? searchString, Dictionary<string, string> filters)
        {
            ApplyFilters(searchString, filters);
        }

        /// <summary>
        /// Конструктор для ВЫБОРКИ данных (с пагинацией и СОРТИРОВКОЙ)
        /// </summary>
        public ContactSearchSpecification(
            string? searchString, 
            Dictionary<string, string> filters, 
            int pageNumber, 
            int pageSize, 
            string? sortOrder)
        {
            ApplyFilters(searchString, filters);
            
            // Подгружаем связанные данные для отображения в таблице
            AddInclude(c => c.Phones);
            AddInclude(c => c.Emails);

            // --- ЛОГИКА СОРТИРОВКИ ---
            if (string.IsNullOrEmpty(sortOrder))
            {
                ApplyOrderBy(c => c.FullName); // По умолчанию сортируем по ФИО
            }
            else
            {
                // Проверяем, является ли сортировка убывающей (desc)
                bool isDesc = sortOrder.EndsWith("_desc");
                // Получаем чистый ключ поля (убираем суффикс _desc)
                string key = isDesc ? sortOrder.Substring(0, sortOrder.Length - 5) : sortOrder;

                // Приводим к нижнему регистру для надежного сравнения
                switch (key.ToLower())
                {
                    // --- СТАНДАРТНЫЕ ПОЛЯ ---
                    case "lastname":
                        if (isDesc) ApplyOrderByDescending(c => c.LastName);
                        else ApplyOrderBy(c => c.LastName);
                        break;
                        
                    case "firstname":
                        if (isDesc) ApplyOrderByDescending(c => c.FirstName);
                        else ApplyOrderBy(c => c.FirstName);
                        break;
                        
                    case "middlename":
                        if (isDesc) ApplyOrderByDescending(c => c.MiddleName);
                        else ApplyOrderBy(c => c.MiddleName);
                        break;

                    // --- СЛОЖНЫЕ ПОЛЯ ---
                    
                    // 1. Составное поле ФИО
                    case "fullname":
                        // сортируем по Фамилии.
                        if (isDesc) 
                        {
                            ApplyOrderByDescending(c => c.LastName);
                        }
                        else 
                        {
                            ApplyOrderBy(c => c.LastName);
                        }
                        break;

                    // 2. Связанные данные: Телефон (Сортировка по первому номеру)
                    case "phone":
                        if (isDesc) ApplyOrderByDescending(c => c.Phones.Select(p => p.Number).FirstOrDefault());
                        else ApplyOrderBy(c => c.Phones.Select(p => p.Number).FirstOrDefault());
                        break;

                    // 3. Связанные данные: Email (Сортировка по первому Email)
                    case "email":
                        if (isDesc) ApplyOrderByDescending(c => c.Emails.Select(e => e.Email).FirstOrDefault());
                        else ApplyOrderBy(c => c.Emails.Select(e => e.Email).FirstOrDefault());
                        break;

                    // --- ДИНАМИЧЕСКИЕ ПОЛЯ (JSON) ---
                    default:
                        // Используем EF.Functions для извлечения значения из JSON и сортировки по нему
                        // ВАЖНО: Сортировка происходит как по строкам. Числа в JSON тоже сортируются как строки.
                        if (isDesc)
                        {
                            ApplyOrderByDescending(c => NpgsqlJsonExtensions.JsonExtractPathText(c.Properties, key));
                        }
                        else
                        {
                            ApplyOrderBy(c => NpgsqlJsonExtensions.JsonExtractPathText(c.Properties, key));
                        }
                        break;
                }
            }

            ApplyPaging((pageNumber - 1) * pageSize, pageSize);
        }

        /// <summary>
        /// Применяет фильтры поиска (общий поиск + детальные фильтры)
        /// </summary>
        private void ApplyFilters(string? searchString, Dictionary<string, string> filters)
        {
            // 1. Глобальный поиск (строка поиска)
            if (!string.IsNullOrEmpty(searchString))
            {
                var s = searchString.Trim();
                AddCriteria(c => 
                    EF.Functions.ILike(c.FullName, $"%{s}%") ||
                    c.Phones.Any(p => EF.Functions.ILike(p.Number, $"%{s}%")) ||
                    c.Emails.Any(e => EF.Functions.ILike(e.Email, $"%{s}%"))
                );
            }

            // 2. Детальные фильтры (по колонкам)
            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    var key = filter.Key.StartsWith("f_") ? filter.Key.Substring(2) : filter.Key;
                    var val = filter.Value?.Trim();

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
                        case "Phone": 
                            AddCriteria(c => c.Phones.Any(p => EF.Functions.ILike(p.Number, $"%{val}%")));
                            break;
                        case "Email": 
                            AddCriteria(c => c.Emails.Any(e => EF.Functions.ILike(e.Email, $"%{val}%")));
                            break;
                        default:
                            // Фильтрация по динамическим полям JSON
                            AddCriteria(c => c.Properties != null && 
                                EF.Functions.ILike(NpgsqlJsonExtensions.JsonExtractPathText(c.Properties, key), $"%{val}%"));
                            break;
                    }
                }
            }
        }
    }
}