using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CIEL.Reconciliation.Services;

public static class NameTools
{
    private static readonly HashSet<string> Titles = new(StringComparer.OrdinalIgnoreCase)
    { "mr", "mrs", "ms", "miss", "dr", "prof", "sir", "sheikh", "shaikh" };

    /// <summary>
    /// Converts common Arabic/Persian and Cyrillic characters to Latin letters.
    /// This is transliteration for matching, not a legal translation of the guest's name.
    /// The original name is always preserved for display and export.
    /// </summary>
    public static string Transliterate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var source = value.Normalize(NormalizationForm.FormD);
        var output = new StringBuilder(source.Length * 2);

        foreach (var ch in source)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            output.Append(ch switch
            {
                // Arabic and Persian letters
                'ا' or 'أ' or 'إ' or 'آ' or 'ٱ' => "a",
                'ب' => "b",
                'ت' => "t",
                'ث' => "th",
                'ج' => "j",
                'ح' => "h",
                'خ' => "kh",
                'د' => "d",
                'ذ' => "dh",
                'ر' => "r",
                'ز' => "z",
                'س' => "s",
                'ش' => "sh",
                'ص' => "s",
                'ض' => "d",
                'ط' => "t",
                'ظ' => "z",
                'ع' => "a",
                'غ' => "gh",
                'ف' => "f",
                'ق' => "q",
                'ك' or 'ک' => "k",
                'ل' => "l",
                'م' => "m",
                'ن' => "n",
                'ه' or 'ة' => "h",
                'و' or 'ؤ' => "w",
                'ي' or 'ى' or 'ئ' or 'ی' => "y",
                'ء' => "",
                'پ' => "p",
                'چ' => "ch",
                'ژ' => "zh",
                'گ' => "g",

                // Russian/Ukrainian and common Cyrillic letters
                'А' or 'а' => "a", 'Б' or 'б' => "b", 'В' or 'в' => "v",
                'Г' or 'г' => "g", 'Ґ' or 'ґ' => "g", 'Д' or 'д' => "d",
                'Е' or 'е' => "e", 'Ё' or 'ё' => "yo", 'Є' or 'є' => "ye",
                'Ж' or 'ж' => "zh", 'З' or 'з' => "z", 'И' or 'и' => "i",
                'І' or 'і' => "i", 'Ї' or 'ї' => "yi", 'Й' or 'й' => "y",
                'К' or 'к' => "k", 'Л' or 'л' => "l", 'М' or 'м' => "m",
                'Н' or 'н' => "n", 'О' or 'о' => "o", 'П' or 'п' => "p",
                'Р' or 'р' => "r", 'С' or 'с' => "s", 'Т' or 'т' => "t",
                'У' or 'у' => "u", 'Ф' or 'ф' => "f", 'Х' or 'х' => "kh",
                'Ц' or 'ц' => "ts", 'Ч' or 'ч' => "ch", 'Ш' or 'ш' => "sh",
                'Щ' or 'щ' => "shch", 'Ы' or 'ы' => "y", 'Э' or 'э' => "e",
                'Ю' or 'ю' => "yu", 'Я' or 'я' => "ya", 'Ь' or 'ь' or 'Ъ' or 'ъ' => "",

                _ => ch.ToString()
            });
        }

        return output.ToString();
    }

    public static string Normalize(string? value)
    {
        var text = Transliterate(value).ToLowerInvariant().Replace(',', ' ');
        var tokens = Regex.Matches(text, "[a-z0-9]+")
            .Select(m => SimplifyToken(m.Value))
            .Where(t => t.Length > 0 && !Titles.Contains(t))
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToArray();
        return string.Join(' ', tokens);
    }

    private static string SimplifyToken(string token)
    {
        // Reduce spelling variation without changing the displayed guest name.
        var value = token;
        value = Regex.Replace(value, "(.)\\1{2,}", "$1$1");
        value = value.Replace("ph", "f", StringComparison.Ordinal)
                     .Replace("ou", "u", StringComparison.Ordinal)
                     .Replace("oo", "u", StringComparison.Ordinal)
                     .Replace("ee", "i", StringComparison.Ordinal);
        return value;
    }
}
