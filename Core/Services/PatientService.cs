using System.Text;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Core.Services
{
    public class SearchResult
    {
        public string Message { get; set; } = "";
        public int Count { get; set; }
        public bool IsTooMany { get; set; }
    }

    public class PatientService
    {
        // 1. Поле для хранения контекста БД
        private readonly AppDbContext _db;
        
        // 2. Конструктор, через который DI-контейнер передает базу
        public PatientService(AppDbContext db)
        {
            _db = db;
        }
        
        private const int MaxAutoShowResults = 15;

        public SearchResult Search(string query, bool forceShowAll)
        {
            string searchKey = query.Replace(" ", "").ToUpper();
            
            // Загружаем пациентов И их визиты
            var patients = _db.Patients
                .Include(p => p.Visits)
                .Where(p => p.NormalizedName.Contains(searchKey))
                .ToList();

            // Считаем визиты
            int totalVisitsCount = patients.Sum(p => p.Visits.Count);

            if (totalVisitsCount == 0) return new SearchResult { Message = "Ничего не найдено в базе данных." };

            if (!forceShowAll && totalVisitsCount > MaxAutoShowResults)
            {
                return new SearchResult { Count = totalVisitsCount, IsTooMany = true };
            }

            StringBuilder sb = new StringBuilder();
            decimal totalGlobalSum = 0;

            foreach (var p in patients)
            {
                // Добавим информацию о пациенте (Карта и Телефон) в начало блока
                sb.AppendLine($"👤 **{p.FullName}**");
                if (!string.IsNullOrEmpty(p.CardNumber)) sb.AppendLine($"💳 Карта №: {p.CardNumber}");
                if (!string.IsNullOrEmpty(p.PhoneNumber)) sb.AppendLine($"📞 Тел: {p.PhoneNumber}");
                if (!string.IsNullOrEmpty(p.Comment)) sb.AppendLine($"📝 Заметка: {p.Comment}");
                sb.AppendLine("--- Визиты ---");

                foreach (var v in p.Visits.OrderByDescending(v => v.Date))
                {
                    totalGlobalSum += v.TotalCost;
                    sb.AppendLine($"📅 {v.Date:dd.MM.yyyy} — {v.ServiceName}");
                    sb.AppendLine($"💰 {v.TotalCost:N0} руб.");
                    sb.AppendLine("➖➖➖➖➖➖");
                }
                sb.AppendLine(); // Разделитель между разными пациентами
            }

            var header = $"🔎 Найдено записей: {totalVisitsCount} (Пациентов: {patients.Count})\n💰 Всего оплачено: {totalGlobalSum:N0} руб.\n\n";
            string finalMsg = header + sb.ToString();

            if (finalMsg.Length > 4000) 
                finalMsg = finalMsg.Substring(0, 4000) + "\n\n...(список обрезан)...";

            return new SearchResult { Message = finalMsg, Count = totalVisitsCount, IsTooMany = false };
        }
    }
}