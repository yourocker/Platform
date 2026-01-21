using System;
using System.Linq;
using Core.Data;
using Core.Entities.System;
using Microsoft.EntityFrameworkCore;

namespace Core.Services
{
    public interface ICrmStyleService
    {
        UiSettings GetSettings();
    }

    public class CrmStyleService : ICrmStyleService
    {
        private readonly AppDbContext _context;
        
        // Статический кэш, чтобы не нагружать базу данных на каждом рендеринге кнопки
        private static UiSettings _cachedSettings;

        public CrmStyleService(AppDbContext context)
        {
            _context = context;
        }

        public UiSettings GetSettings()
        {
            if (_cachedSettings == null)
            {
                // Ищем глобальные настройки (где EmployeeId == null)
                _cachedSettings = _context.UiSettings
                    .AsNoTracking()
                    .FirstOrDefault(s => s.EmployeeId == null);

                // Если в базе еще нет записей, возвращаем новый объект с дефолтами из вашего класса
                if (_cachedSettings == null)
                {
                    _cachedSettings = new UiSettings();
                }
            }
            return _cachedSettings;
        }
        
        public static void ClearCache() => _cachedSettings = null;
    }
}