using System.Globalization;
using System.Text;

namespace RecipeShopper.Infrastructure.Persistence;

internal static class TextNormalization
{
    public static string NameKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Normalize(NormalizationForm.FormC).Trim();
        var builder = new StringBuilder(normalized.Length);
        var previousWasWhitespace = false;

        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
            }
            else
            {
                builder.Append(character);
                previousWasWhitespace = false;
            }
        }

        return builder.ToString().ToUpper(CultureInfo.InvariantCulture);
    }
}
