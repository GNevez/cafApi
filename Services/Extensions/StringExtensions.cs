using System.Text.RegularExpressions;

namespace cafApi.Services.Extensions
{
    public static class StringExtensions
    {
        public static string GenerateSlug(this string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Converter para minúsculas
            text = text.ToLowerInvariant();

            // Remover acentos
            text = RemoveAccents(text);

            // Substituir espaços e caracteres especiais por hífens
            text = Regex.Replace(text, @"[^a-z0-9\s-]", "");
            text = Regex.Replace(text, @"[\s-]+", "-");

            // Remover hífens do início e fim
            text = text.Trim('-');

            return text;
        }

        private static string RemoveAccents(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Usar uma abordagem mais simples para remover acentos
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}

