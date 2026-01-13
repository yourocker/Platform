using System.Text;
using System.Text.RegularExpressions;
using Core.Data;
using Core.Entities;
using Core.Entities.ol;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;

namespace Core.Services
{
    public class PatientImporter
    {
        private readonly AppDbContext _db;

        public PatientImporter(AppDbContext db)
        {
            _db = db;
        }

        public async Task<string> ImportAsync(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Синхронизация мастер-базы пациентов");

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var result = reader.AsDataSet();
            var table = result.Tables[0]; // Берем первый лист

            var updated = 0;
            var created = 0;

            // Начинаем со 2-й строки (пропускаем заголовок)
            for (int i = 1; i < table.Rows.Count; i++)
            {
                var row = table.Rows[i];
                var fio = row[0]?.ToString()?.Trim() ?? "";
                var card = row[1]?.ToString()?.Trim() ?? "";
                var phoneRaw = row[2]?.ToString()?.Trim() ?? "";
                var comment = row[3]?.ToString()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(fio) || fio == "осотов") continue;

                var normalized = fio.ToUpper().Replace(" ", "").Replace(".", "");
                var cleanPhone = CleanPhoneNumber(phoneRaw);

                // Ищем: сначала по номеру карты, если он есть, затем по нормализованному имени
                var patient = await _db.Patients
                    .FirstOrDefaultAsync(p => (card != "" && p.CardNumber == card) || p.NormalizedName == normalized);

                if (patient == null)
                {
                    patient = new Patient
                    {
                        Id = Guid.NewGuid(),
                        FullName = fio,
                        NormalizedName = normalized,
                        CardNumber = card,
                        PhoneNumber = cleanPhone,
                        Comment = comment
                    };
                    _db.Patients.Add(patient);
                    created++;
                }
                else
                {
                    // Обновляем данные, если они изменились или были пустыми
                    patient.CardNumber = card;
                    if (!string.IsNullOrEmpty(cleanPhone)) patient.PhoneNumber = cleanPhone;
                    if (!string.IsNullOrEmpty(comment)) patient.Comment = comment;
                    updated++;
                }
            }

            await _db.SaveChangesAsync();
            sb.AppendLine($"✅ Готово. Создано: {created}, Обновлено: {updated}");
            return sb.ToString();
        }

        private string CleanPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "";
            // Оставляем только цифры
            var digits = Regex.Replace(phone, @"[^\d]", "");
            // Если начинается с 8, меняем на 7
            if (digits.StartsWith("8") && digits.Length == 11) digits = "7" + digits.Substring(1);
            return digits;
        }
    }
}