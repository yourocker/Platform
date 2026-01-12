using Telegram.Bot.Types.ReplyMarkups;

namespace Core.Bot;

public static class Keyboards
{
    public static ReplyKeyboardMarkup GetMainMenu()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "🔍 Поиск визитов" },
            new KeyboardButton[] { "📅 Кассовый отчет за день", "💰 Отчет по выручке (период)" },
            new KeyboardButton[] { "🔄 Обновить базу" }
        })
        {
            ResizeKeyboard = true
        };
    }

    public static ReplyKeyboardMarkup GetCancelKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton("❌ Отмена")
        })
        {
            ResizeKeyboard = true
        };
    }
}