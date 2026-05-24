using System.Globalization;
using System.Text;

namespace Hexalith.Folders.Providers.Abstractions;

public static class ProviderIdentityIdentifier
{
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string trimmed = value.Trim().Normalize(NormalizationForm.FormC).ToLower(CultureInfo.InvariantCulture);
        StringBuilder builder = new(trimmed.Length);
        bool separatorPending = false;

        foreach (char character in trimmed)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                if (separatorPending && builder.Length > 0)
                {
                    builder.Append('_');
                }

                builder.Append(character);
                separatorPending = false;
                continue;
            }

            if (character is '-' or '_' or '.' || char.IsWhiteSpace(character))
            {
                separatorPending = true;
                continue;
            }

            throw new ArgumentException("Provider identity contains unsupported characters.", nameof(value));
        }

        return builder.Length == 0 ? throw new ArgumentException("Provider identity is empty.", nameof(value)) : builder.ToString();
    }
}
