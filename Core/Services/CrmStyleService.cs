using System;
using System.Linq;
using System.Threading.Tasks;
using Core.Data;
using Core.Entities.System;
using Microsoft.EntityFrameworkCore;

namespace Core.Services
{
    public interface ICrmStyleService
    {
        /// <summary>
        /// Получает текущие настройки интерфейса.
        /// </summary>
        UiSettings GetSettings();

        /// <summary>
        /// Сохраняет настройки в базу данных и сбрасывает кэш.
        /// </summary>
        Task SaveSettingsAsync(UiSettings settings);
    }

    public class CrmStyleService : ICrmStyleService
    {
        private readonly AppDbContext _context;

        public CrmStyleService(AppDbContext context)
        {
            _context = context;
        }

        public UiSettings GetSettings()
        {
            // Ищем глобальные настройки текущего tenant-а (где EmployeeId == null)
            var settings = _context.UiSettings
                .AsNoTracking()
                .FirstOrDefault(s => s.EmployeeId == null);

            return settings ?? new UiSettings();
        }

        public async Task SaveSettingsAsync(UiSettings settings)
        {
            // Ищем существующую глобальную запись в БД
            var dbEntry = await _context.UiSettings
                .FirstOrDefaultAsync(s => s.EmployeeId == null);

            if (dbEntry == null)
            {
                // Если настроек еще нет, создаем новую запись
                dbEntry = new UiSettings
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = null
                };
                _context.UiSettings.Add(dbEntry);
            }

            // --- БРЕНД И АКЦЕНТЫ ---
            dbEntry.PrimaryColor = settings.PrimaryColor;
            dbEntry.PrimaryContentColor = settings.PrimaryContentColor;
            dbEntry.AccentColor = settings.AccentColor;

            // --- ЛЕВОЕ МЕНЮ 1 (SIDEBAR) ---
            dbEntry.MainBgColor = settings.MainBgColor;
            dbEntry.MenuTextColor = settings.MenuTextColor;

            // --- ЛЕВОЕ МЕНЮ 2 (SUB-MENU) ---
            dbEntry.SubMenuBgColor = settings.SubMenuBgColor;
            dbEntry.SubMenuTextColor = settings.SubMenuTextColor;

            // --- РАБОЧАЯ ОБЛАСТЬ ---
            dbEntry.PageBgColor = settings.PageBgColor;
            dbEntry.PageTextColor = settings.PageTextColor;

            // --- КАРТОЧКИ И ТАБЛИЦЫ ---
            dbEntry.CardBgColor = settings.CardBgColor;
            dbEntry.CardTextColor = settings.CardTextColor;

            // --- СИСТЕМНЫЕ НАСТРОЙКИ ---
            dbEntry.BaseFontSize = settings.BaseFontSize;
            
            // Обновляем путь к логотипу только если он явно передан
            if (settings.LogoPath != null)
            {
                dbEntry.LogoPath = settings.LogoPath;
            }

            dbEntry.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }
    }
}
