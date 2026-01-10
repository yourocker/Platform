using System.Collections.Generic;
using System.Linq;
using MedicalBot.Data;
using MedicalBot.Entities;
using MedicalBot.Entities.Platform; // Нужно для AppFieldDefinition
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MedicalWeb.Controllers
{
    public abstract class BasePlatformController : Controller
    {
        protected readonly AppDbContext _context;

        protected BasePlatformController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Загружает определения полей для указанной сущности и кладет их во ViewBag.
        /// Используется в GET методах (Create, Edit).
        /// </summary>
        protected async Task LoadDynamicFields(string systemCode)
        {
            var fields = await _context.AppFieldDefinitions
                .Where(f => f.AppDefinition.EntityCode == systemCode)
                .OrderBy(f => f.SortOrder)
                .ToListAsync();

            ViewBag.DynamicFields = fields;
        }

        /// <summary>
        /// Умное сохранение: читает IFormCollection, проверяет настройки полей (IsArray, DataType)
        /// и формирует корректный JSON.
        /// </summary>
        protected async Task SaveDynamicProperties(IHasDynamicProperties entity, IFormCollection form, string entityCode)
        {
            // 1. Загружаем определения полей, чтобы понимать типы данных
            var definitions = await _context.AppFieldDefinitions
                .Where(f => f.AppDefinition.EntityCode == entityCode)
                .ToListAsync();

            var resultDictionary = new Dictionary<string, object>();

            foreach (var def in definitions)
            {
                // Ключ в форме: DynamicProps[SystemName]
                var formKey = $"DynamicProps[{def.SystemName}]";

                if (form.ContainsKey(formKey))
                {
                    var formValues = form[formKey];

                    if (def.IsArray)
                    {
                        // Если поле - массив, сохраняем все значения
                        // (например, мульти-выбор или несколько файлов)
                        var values = formValues.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
                        resultDictionary[def.SystemName] = values;
                    }
                    else
                    {
                        // Одиночное значение
                        if (def.DataType == FieldDataType.Boolean)
                        {
                            // Особенность чекбоксов в HTML: часто шлют "true,false"
                            resultDictionary[def.SystemName] = formValues.Any(v => v == "true").ToString().ToLower();
                        }
                        else
                        {
                            resultDictionary[def.SystemName] = formValues.FirstOrDefault() ?? "";
                        }
                    }
                }
            }

            // 2. Сериализуем итоговый словарь в JSON
            var options = new JsonSerializerOptions 
            { 
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
            };
            
            entity.Properties = JsonSerializer.Serialize(resultDictionary, options);
        }
    }
}