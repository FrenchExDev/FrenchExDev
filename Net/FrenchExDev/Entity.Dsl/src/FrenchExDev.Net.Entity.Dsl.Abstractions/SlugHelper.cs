namespace FrenchExDev.Net.Entity.Dsl.Abstractions;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>URL slug generation helper for <c>[Sluggable]</c> behavior.</summary>
public static class SlugHelper
{
    public static string ToSlug(string input, string separator = "-", bool lowercase = true, bool transliterate = true)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var result = input;

        if (transliterate)
            result = RemoveDiacritics(result);

        if (lowercase)
            result = result.ToLowerInvariant();

        // Replace non-alphanumeric characters with separator
        result = Regex.Replace(result, @"[^a-zA-Z0-9\s-]", string.Empty);
        result = Regex.Replace(result, @"[\s]+", separator);
        result = Regex.Replace(result, Regex.Escape(separator) + "+", separator);
        result = result.Trim(separator[0]);

        return result;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
