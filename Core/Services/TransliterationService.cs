using System.Text;
using System.Text.RegularExpressions;
using Core.Interfaces;

namespace Core.Services;

public class TransliterationService : ITransliterationService
{
    // Словарь согласно ГОСТ 7.79-2000 (Система Б)
    private static readonly Dictionary<char, string> GostMap = new()
    {
        {'а', "a"}, {'б', "b"}, {'в', "v"}, {'г', "g"}, {'д', "d"},
        {'е', "e"}, {'ё', "yo"}, {'ж', "zh"}, {'з', "z"}, {'и', "i"},
        {'й', "j"}, {'к', "k"}, {'л', "l"}, {'м', "m"}, {'н', "n"},
        {'о', "o"}, {'п', "p"}, {'р', "r"}, {'с', "s"}, {'т', "t"},
        {'у', "u"}, {'ф', "f"}, {'х', "x"}, {'ц', "cz"}, {'ч', "ch"},
        {'ш', "sh"}, {'щ', "shch"}, {'ъ', ""}, {'ы', "y"}, {'ь', ""},
        {'э', "e"}, {'ю', "yu"}, {'я', "ya"},
        
        {'А', "A"}, {'Б', "B"}, {'В', "V"}, {'Г', "G"}, {'Д', "D"},
        {'Е', "E"}, {'Ё', "Yo"}, {'Ж', "Zh"}, {'З', "Z"}, {'И', "I"},
        {'Й', "J"}, {'К', "K"}, {'Л', "L"}, {'М', "M"}, {'Н', "N"},
        {'О', "O"}, {'П', "P"}, {'Р', "R"}, {'С', "S"}, {'Т', "T"},
        {'У', "U"}, {'Ф', "F"}, {'Х', "X"}, {'Ц', "Cz"}, {'Ч', "Ch"},
        {'Ш', "Sh"}, {'Щ', "Shch"}, {'Ъ', ""}, {'Ы', "Y"}, {'Ь', ""},
        {'Э', "E"}, {'Ю', "Yu"}, {'Я', "Ya"}
    };

    public string TransliterateToSystemName(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        var sb = new StringBuilder();

        foreach (var c in source)
        {
            if (GostMap.TryGetValue(c, out var mapped))
            {
                sb.Append(mapped);
            }
            else if (char.IsLetterOrDigit(c))
            {
                // Оставляем латиницу и цифры как есть
                sb.Append(c);
            }
            else if (char.IsWhiteSpace(c) || c == '-' || c == '_')
            {
                // Пробелы и разделители меняем на подчеркивание
                sb.Append('_');
            }
            // Остальные символы (спецсимволы) игнорируем
        }

        var result = sb.ToString();

        // Убираем повторяющиеся подчеркивания и лишние подчеркивания по краям
        // Regex Compiled для производительности, так как метод будет вызываться часто
        result = Regex.Replace(result, @"_+", "_");
        result = result.Trim('_');

        return result;
    }
}