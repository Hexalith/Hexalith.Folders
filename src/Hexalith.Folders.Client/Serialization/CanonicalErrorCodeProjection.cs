using System.Reflection;
using System.Runtime.Serialization;

using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Client.Serialization;

/// <summary>Projects the generated closed canonical error-code enum to its wire token.</summary>
public static class CanonicalErrorCodeProjection
{
    /// <summary>Gets the exact <see cref="EnumMemberAttribute.Value"/> declared by the generated contract.</summary>
    public static string WireValue(CanonicalErrorCode code)
    {
        string memberName = code.ToString();
        FieldInfo field = typeof(CanonicalErrorCode).GetField(memberName)
            ?? throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown canonical error code.");
        return field.GetCustomAttribute<EnumMemberAttribute>()?.Value
            ?? throw new InvalidOperationException($"Canonical error code '{memberName}' has no wire value.");
    }
}
