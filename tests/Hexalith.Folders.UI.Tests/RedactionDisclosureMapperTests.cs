using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.4 / AC #5 — totality coverage for <see cref="RedactionDisclosureMapper"/>. Each
/// <c>[Theory]</c> enumerates <c>Enum.GetValues&lt;TEnum&gt;()</c> so a future SDK enum addition the
/// mapper does not handle fails the test (the mapper throws on the new value). There is intentionally
/// no cross-side parity sentinel here — these are pure SDK→presentation maps owned solely by the UI.
/// </summary>
public sealed class RedactionDisclosureMapperTests
{
    public static TheoryData<RedactionMetadataVisibility> AllVisibilities() => ToTheory<RedactionMetadataVisibility>();

    public static TheoryData<RedactableAuditTimestampPrecision> AllPrecisions() => ToTheory<RedactableAuditTimestampPrecision>();

    public static TheoryData<FileMetadataItemRedaction> AllFileRedactions() => ToTheory<FileMetadataItemRedaction>();

    public static TheoryData<DiagnosticFieldClassification, bool> AllClassificationsWithHasValue()
    {
        TheoryData<DiagnosticFieldClassification, bool> data = new();
        foreach (DiagnosticFieldClassification value in Enum.GetValues<DiagnosticFieldClassification>())
        {
            data.Add(value, true);
            data.Add(value, false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllVisibilities))]
    public void FromAuditVisibility_IsTotal_ForEveryRedactionMetadataVisibility(RedactionMetadataVisibility visibility)
    {
        FieldDisclosure expected = visibility switch
        {
            RedactionMetadataVisibility.Metadata_only => FieldDisclosure.Visible,
            RedactionMetadataVisibility.Redacted => FieldDisclosure.Redacted,
            _ => throw new InvalidOperationException($"Unhandled visibility {visibility} — mapper coverage drifted."),
        };

        RedactionDisclosureMapper.FromAuditVisibility(visibility).ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(AllPrecisions))]
    public void FromTimestampPrecision_IsTotal_ForEveryPrecision(RedactableAuditTimestampPrecision precision)
    {
        FieldDisclosure expected = precision switch
        {
            RedactableAuditTimestampPrecision.Exact => FieldDisclosure.Visible,
            RedactableAuditTimestampPrecision.Bucketed => FieldDisclosure.Visible,
            RedactableAuditTimestampPrecision.Redacted => FieldDisclosure.Redacted,
            _ => throw new InvalidOperationException($"Unhandled precision {precision} — mapper coverage drifted."),
        };

        RedactionDisclosureMapper.FromTimestampPrecision(precision).ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(AllFileRedactions))]
    public void FromFileMetadataRedaction_IsTotal_ForEveryRedaction(FileMetadataItemRedaction redaction)
    {
        FieldDisclosure expected = redaction switch
        {
            FileMetadataItemRedaction.Not_redacted => FieldDisclosure.Visible,
            FileMetadataItemRedaction.Redacted => FieldDisclosure.Redacted,
            FileMetadataItemRedaction.Excluded => FieldDisclosure.Missing,
            FileMetadataItemRedaction.Binary_disallowed => FieldDisclosure.Missing,
            _ => throw new InvalidOperationException($"Unhandled redaction {redaction} — mapper coverage drifted."),
        };

        RedactionDisclosureMapper.FromFileMetadataRedaction(redaction).ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(AllClassificationsWithHasValue))]
    public void FromDiagnosticClassification_MapsForbiddenToRedacted_AndHonorsHasValue(
        DiagnosticFieldClassification classification,
        bool hasValue)
    {
        FieldDisclosure expected = classification switch
        {
            DiagnosticFieldClassification.Forbidden => FieldDisclosure.Redacted,
            DiagnosticFieldClassification.Consumer_safe => hasValue ? FieldDisclosure.Visible : FieldDisclosure.Missing,
            DiagnosticFieldClassification.Operator_sanitized => hasValue ? FieldDisclosure.Visible : FieldDisclosure.Missing,
            _ => throw new InvalidOperationException($"Unhandled classification {classification} — mapper coverage drifted."),
        };

        RedactionDisclosureMapper.FromDiagnosticClassification(classification, hasValue).ShouldBe(expected);
    }

    [Fact]
    public void FromDiagnosticClassification_ForbiddenIsRedacted_RegardlessOfHasValue()
    {
        RedactionDisclosureMapper.FromDiagnosticClassification(DiagnosticFieldClassification.Forbidden, hasValue: true)
            .ShouldBe(FieldDisclosure.Redacted);
        RedactionDisclosureMapper.FromDiagnosticClassification(DiagnosticFieldClassification.Forbidden, hasValue: false)
            .ShouldBe(FieldDisclosure.Redacted);
    }

    [Fact]
    public void FromAuditRedaction_ResolvesRedactedThenValuePresence()
    {
        RedactionMetadata redacted = new() { Visibility = RedactionMetadataVisibility.Redacted };
        RedactionMetadata metadataOnly = new() { Visibility = RedactionMetadataVisibility.Metadata_only };

        // Redaction wins even if a value leaked in.
        RedactionDisclosureMapper.FromAuditRedaction(redacted, null).ShouldBe(FieldDisclosure.Redacted);
        RedactionDisclosureMapper.FromAuditRedaction(redacted, "x").ShouldBe(FieldDisclosure.Redacted);

        // Not redacted: presence of a value decides visible vs missing.
        RedactionDisclosureMapper.FromAuditRedaction(metadataOnly, null).ShouldBe(FieldDisclosure.Missing);
        RedactionDisclosureMapper.FromAuditRedaction(metadataOnly, string.Empty).ShouldBe(FieldDisclosure.Missing);
        RedactionDisclosureMapper.FromAuditRedaction(metadataOnly, "x").ShouldBe(FieldDisclosure.Visible);
    }

    [Fact]
    public void FromAuditRedaction_Throws_OnNullMetadata()
        => Should.Throw<ArgumentNullException>(() => RedactionDisclosureMapper.FromAuditRedaction(null!, "x"));

    [Theory]
    [MemberData(nameof(AllVisibilities))]
    public void FromAuditRedaction_IsTotal_ForEveryRedactionMetadataVisibility(RedactionMetadataVisibility visibility)
    {
        // Parity with the other four overloads' totality sentinels: FromAuditRedaction also consumes
        // RedactionMetadataVisibility (via RedactionMetadata.Visibility), so a future enum member must
        // force a conscious mapping decision here too — never a silent "treat as not-redacted" value leak
        // on the redaction surface (the failure mode the mapper's design exists to prevent).
        RedactionMetadata redaction = new() { Visibility = visibility };
        FieldDisclosure expected = visibility switch
        {
            RedactionMetadataVisibility.Redacted => FieldDisclosure.Redacted,
            RedactionMetadataVisibility.Metadata_only => FieldDisclosure.Visible,
            _ => throw new InvalidOperationException($"Unhandled visibility {visibility} — FromAuditRedaction coverage drifted."),
        };

        // A present value is supplied so Metadata_only resolves to Visible (not Missing); redaction still wins for Redacted.
        RedactionDisclosureMapper.FromAuditRedaction(redaction, "present-value").ShouldBe(expected);
    }

    private static TheoryData<TEnum> ToTheory<TEnum>()
        where TEnum : struct, Enum
    {
        TheoryData<TEnum> data = new();
        foreach (TEnum value in Enum.GetValues<TEnum>())
        {
            data.Add(value);
        }

        return data;
    }
}
