using System.Text.Json;
using MedicalBot.Configuration;
using MedicalBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MedicalBot.Bot;

public class UpdateHandler
{
    private readonly AppConfig _config;
    private readonly PatientService _patientService;
    private readonly StatisticsService _statsService;
    private readonly ExcelImporter _excelImporter;
    private readonly AppointmentImporter _appointmentImporter;
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly PatientImporter _patientImporter;

    // Состояния пользователей и временные данные
    private static readonly Dictionary<long, UserState> _userStates = new();
    private static readonly Dictionary<long, DateTime> _tempStartDates = new();

    public enum UserState
    {
        None,
        WaitingForPatientName,
        WaitingForDailyReportDate,
        WaitingForStartDate,
        WaitingForEndDate
    }

    public UpdateHandler(
        AppConfig config, 
        PatientService patientService, 
        StatisticsService statsService,
        ExcelImporter excelImporter,
        AppointmentImporter appointmentImporter,
        PatientImporter patientImporter)
    {
        _config = config;
        _patientService = patientService;
        _statsService = statsService;
        _excelImporter = excelImporter;
        _appointmentImporter = appointmentImporter;
        _patientImporter = patientImporter; 
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        try
        {
            if (update.Message is not { } message) return;
            var userId = message.From.Id;
            var chatId = message.Chat.Id;

            // 1. Проверка доступа
            if (!_config.DirectorIds.Contains(userId))
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId, 
                    text: $"⛔ Нет доступа. Ваш ID: {userId}", 
                    cancellationToken: ct);
                return;
            }

            // 2. Обработка файлов (Ручной импорт при пересылке файла в чат)
            if (message.Type == MessageType.Document && message.Document != null)
            {
                await HandleDocument(botClient, message, ct);
                return;
            }

            if (message.Text is not { } messageText) return;

            // 3. Обработка команд меню
            if (!_userStates.ContainsKey(userId)) _userStates[userId] = UserState.None;

            if (messageText == "❌ Отмена" || messageText == "/start")
            {
                _userStates[userId] = UserState.None;
                await botClient.SendTextMessageAsync(
                    chatId: chatId, 
                    text: messageText == "/start" ? "👋 Добро пожаловать!" : "🔙 Отменено.", 
                    replyMarkup: Keyboards.GetMainMenu(), 
                    cancellationToken: ct);
                return;
            }

            // Переключатель состояний по кнопкам основного меню
            switch (messageText)
            {
                case "🔍 Поиск визитов":
                    _userStates[userId] = UserState.WaitingForPatientName;
                    await botClient.SendTextMessageAsync(
                        chatId: chatId, 
                        text: "✍️ Введите **ФИО** пациента:", 
                        parseMode: ParseMode.Markdown, 
                        replyMarkup: Keyboards.GetCancelKeyboard(), 
                        cancellationToken: ct);
                    return;

                case "📅 Кассовый отчет за день":
                    _userStates[userId] = UserState.WaitingForDailyReportDate;
                    await botClient.SendTextMessageAsync(
                        chatId: chatId, 
                        text: "📅 Введите дату (ДД.ММ.ГГГГ):", 
                        replyMarkup: Keyboards.GetCancelKeyboard(), 
                        cancellationToken: ct);
                    return;

                case "💰 Отчет по выручке (период)":
                    _userStates[userId] = UserState.WaitingForStartDate;
                    await botClient.SendTextMessageAsync(
                        chatId: chatId, 
                        text: "📅 Введите дату начала (ДД.ММ.ГГГГ):", 
                        replyMarkup: Keyboards.GetCancelKeyboard(), 
                        cancellationToken: ct);
                    return;

                case "🔄 Обновить базу":
                    await ProcessAutoUpdate(botClient, chatId, ct);
                    return;
            }

            // 4. Логика ввода данных (обработка ответов пользователя)
            if (_userStates[userId] != UserState.None)
            {
                await HandleInput(botClient, message, userId, messageText, ct);
            }
            else
            {
                // Если мы здесь, значит состояние None и кнопки не сработали
                // Отправляем "по голове" и возвращаем меню
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "⚠️ Пожалуйста, используйте кнопки меню для управления ботом.",
                    replyMarkup: Keyboards.GetMainMenu(),
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($" Ошибка в HandleUpdateAsync: {ex.Message}");
        }
    }

    private async Task ProcessAutoUpdate(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        await bot.SendTextMessageAsync(chatId, "⏳ Начинаю полное обновление из облака...", cancellationToken: ct);

        // 1. СНАЧАЛА ОБНОВЛЯЕМ ПАЦИЕНТОВ (ЭТАЛОН)
        if (!string.IsNullOrEmpty(_config.PatientsUrl))
        {
            await bot.SendTextMessageAsync(chatId, "🧬 Синхронизирую мастер-базу пациентов...", cancellationToken: ct);
            if (await DownloadYandexFileAsync(_config.PatientsUrl, "auto_patients.xlsx"))
            {
                string report = await _patientImporter.ImportAsync("auto_patients.xlsx");
                await bot.SendTextMessageAsync(chatId, report, cancellationToken: ct);
            }
        }

        // 2. ЗАТЕМ ОБНОВЛЯЕМ КАССУ
        if (!string.IsNullOrEmpty(_config.CashUrl))
        {
            await bot.SendTextMessageAsync(chatId, "📥 Скачиваю Кассу...", cancellationToken: ct);
            if (await DownloadYandexFileAsync(_config.CashUrl, "auto_cash.xlsx"))
            {
                string report = await _excelImporter.ImportAsync("auto_cash.xlsx");
                await bot.SendTextMessageAsync(chatId, report, cancellationToken: ct);
            }
        }
    
        await bot.SendTextMessageAsync(chatId, "✅ Все базы синхронизированы.", replyMarkup: Keyboards.GetMainMenu(), cancellationToken: ct);
    }

    private async Task HandleInput(ITelegramBotClient bot, Message msg, long userId, string text, CancellationToken ct)
    {
        var state = _userStates[userId];
        var chatId = msg.Chat.Id;

        if (state == UserState.WaitingForPatientName)
        {
            var result = _patientService.Search(text, false);
            await bot.SendTextMessageAsync(chatId, result.Message, replyMarkup: Keyboards.GetMainMenu(), cancellationToken: ct);
            _userStates[userId] = UserState.None;
        }
        else if (state == UserState.WaitingForDailyReportDate && DateTime.TryParse(text, out var date))
        {
            var report = _statsService.GetPeriodReport(date, date);
            await bot.SendTextMessageAsync(chatId, report, parseMode: ParseMode.Markdown, replyMarkup: Keyboards.GetMainMenu(), cancellationToken: ct);
            _userStates[userId] = UserState.None;
        }
        else if (state == UserState.WaitingForStartDate && DateTime.TryParse(text, out var startDate))
        {
            _tempStartDates[userId] = startDate;
            _userStates[userId] = UserState.WaitingForEndDate;
            await bot.SendTextMessageAsync(chatId, "📅 Введите дату **окончания** (ДД.ММ.ГГГГ):", replyMarkup: Keyboards.GetCancelKeyboard(), cancellationToken: ct);
        }
        else if (state == UserState.WaitingForEndDate && DateTime.TryParse(text, out var endDate))
        {
            var start = _tempStartDates[userId];
            var report = _statsService.GetPeriodReport(start, endDate);
            await bot.SendTextMessageAsync(chatId, report, parseMode: ParseMode.Markdown, replyMarkup: Keyboards.GetMainMenu(), cancellationToken: ct);
            _userStates[userId] = UserState.None;
        }
    }

    private async Task HandleDocument(ITelegramBotClient bot, Message msg, CancellationToken ct)
    {
        var doc = msg.Document;
        var lowerName = doc.FileName?.ToLower() ?? "";
        string chatId = msg.Chat.Id.ToString();

        if (lowerName.Contains("касса"))
        {
            await bot.SendTextMessageAsync(chatId, "💰 Обрабатываю файл КАССЫ...", cancellationToken: ct);
            string path = await DownloadTelegramFile(bot, doc, "manual_cash.xlsx", ct);
            string report = await _excelImporter.ImportAsync(path);
            await bot.SendTextMessageAsync(chatId, report, cancellationToken: ct);
        }
        else if (lowerName.Contains("журнал") || lowerName.Contains("запис"))
        {
            await bot.SendTextMessageAsync(chatId, "📅 Обрабатываю ЖУРНАЛ ЗАПИСИ...", cancellationToken: ct);
            string path = await DownloadTelegramFile(bot, doc, "manual_schedule.xlsx", ct);
            string report = await _appointmentImporter.ImportAsync(path);
            await bot.SendTextMessageAsync(chatId, report, cancellationToken: ct);
        }
    }

    // Хелперы для скачивания
    private async Task<bool> DownloadYandexFileAsync(string publicUrl, string localFileName)
    {
        try
        {
            string apiUrl = $"https://cloud-api.yandex.net/v1/disk/public/resources/download?public_key={System.Net.WebUtility.UrlEncode(publicUrl)}";
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
            var response = await _httpClient.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode) return false;

            string jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            string downloadUrl = doc.RootElement.GetProperty("href").GetString();
            var fileBytes = await _httpClient.GetByteArrayAsync(downloadUrl);
            await System.IO.File.WriteAllBytesAsync(localFileName, fileBytes);
            return true;
        }
        catch { return false; }
    }

    private async Task<string> DownloadTelegramFile(ITelegramBotClient bot, Document doc, string localName, CancellationToken ct)
    {
        var fileInfo = await bot.GetFileAsync(doc.FileId, ct);
        using var fs = System.IO.File.OpenWrite(localName);
        await bot.DownloadFileAsync(fileInfo.FilePath, fs, ct);
        return localName;
    }

    public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($" Ошибка API: {exception.Message}");
        return Task.CompletedTask;
    }
}