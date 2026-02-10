namespace Core.Interfaces;

public interface ITransliterationService
{
    /// <summary>
    /// Преобразует строку в латиницу (транслит) и подготавливает для использования как системный код.
    /// Удаляет спецсимволы, заменяет пробелы на подчеркивания.
    /// </summary>
    /// <param name="source">Исходная строка (например, "Название поля")</param>
    /// <returns>Строка safe-code (например, "Nazvanie_polya")</returns>
    string TransliterateToSystemName(string source);
}