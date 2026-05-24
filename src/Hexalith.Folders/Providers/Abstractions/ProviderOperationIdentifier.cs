using System.Globalization;
using System.Text;

namespace Hexalith.Folders.Providers.Abstractions;

public static class ProviderOperationIdentifier
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["provider_readiness"] = ProviderOperationCatalog.ReadinessValidation,
        ["readiness"] = ProviderOperationCatalog.ReadinessValidation,
        ["provider_support"] = ProviderOperationCatalog.ProviderSupportEvidence,
        ["support_evidence"] = ProviderOperationCatalog.ProviderSupportEvidence,
        ["commit"] = ProviderOperationCatalog.CommitSupport,
        ["branches"] = ProviderOperationCatalog.BranchRefInspection,
        ["refs"] = ProviderOperationCatalog.BranchRefInspection,
    };

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

            if (character is '-' or '_' or '/' || char.IsWhiteSpace(character))
            {
                separatorPending = true;
                continue;
            }

            throw new ArgumentException("Provider operation identifier contains unsupported characters.", nameof(value));
        }

        string canonical = builder.Length == 0 ? throw new ArgumentException("Provider operation identifier is empty.", nameof(value)) : builder.ToString();
        return Aliases.TryGetValue(canonical, out string? alias) ? alias : canonical;
    }
}
