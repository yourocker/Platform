using System.Text;
using MedicalBot.Data;
using Microsoft.EntityFrameworkCore;

namespace MedicalBot.Services
{
    public class StatisticsService
    {
        // 1. Добавляем поле для хранения контекста
        private readonly AppDbContext _db;

        // 2. Добавляем конструктор для DI
        public StatisticsService(AppDbContext db)
        {
            _db = db;
        }

        public string GetPeriodReport(DateTime startDate, DateTime endDate)
        {
            var startUtc = startDate.ToUniversalTime();
            var endUtc = endDate.AddDays(1).Date.ToUniversalTime();

            // ✅ ИСПОЛЬЗУЕМ: _db (вместо db)
            var visitsInPeriod = _db.Visits
                .Where(v => v.Date >= startUtc && v.Date < endUtc)
                .ToList();

            if (!visitsInPeriod.Any())
            {
                return $"📉 За период с {startDate:dd.MM} по {endDate:dd.MM} данных нет.";
            }

            decimal totalRevenue = visitsInPeriod.Sum(v => v.TotalCost);
            decimal totalCash = visitsInPeriod.Sum(v => v.AmountCash);
            decimal totalCashless = visitsInPeriod.Sum(v => v.AmountCashless);

            int visitsCount = visitsInPeriod.Count;
            int uniquePatients = visitsInPeriod.Select(v => v.PatientId).Distinct().Count();

            var sb = new StringBuilder();
            sb.AppendLine($"📊 **Финансовый отчет**");
            sb.AppendLine($"📅 Период: {startDate:dd.MM.yyyy} — {endDate:dd.MM.yyyy}");
            sb.AppendLine("➖➖➖➖➖➖➖➖");
            
            sb.AppendLine($"💰 **ИТОГО: {totalRevenue:N0} руб.**");
            sb.AppendLine($"💵 Наличные: {totalCash:N0} руб.");
            sb.AppendLine($"💳 Безнал: {totalCashless:N0} руб.");
            sb.AppendLine("➖➖➖➖➖➖➖➖");
            
            sb.AppendLine($"👥 Пациентов: {uniquePatients}");
            sb.AppendLine($"🧾 Визитов (чеков): {visitsCount}");
            
            return sb.ToString();
        }
    }
}