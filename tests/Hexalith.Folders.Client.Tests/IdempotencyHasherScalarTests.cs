using Hexalith.Folders.Client.Idempotency;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Client.Tests;

public sealed class IdempotencyHasherScalarTests
{
    [Fact]
    public void CanonicalHasherPinsLegacyJsonFormsForExtendedScalars()
    {
        (object Value, string Canonical)[] vectors =
        [
            ((Half)1.1, "j:\"1.1\""),
            (Half.NegativeZero, "j:\"-0\""),
            (Half.NaN, "j:\"NaN\""),
            (Half.PositiveInfinity, "j:\"Infinity\""),
            (System.Numerics.BigInteger.One << 100, "j:1267650600228229401496703205376"),
            (Int128.MinValue, "j:\"-170141183460469231731687303715884105728\""),
            (UInt128.MaxValue, "j:\"340282366920938463463374607431768211455\""),
            ((nint)(-42), "j:{\"value\":-42}"),
            ((nuint)42, "j:{\"value\":42}"),
            (new Version(1, 2, 3, 4), "j:\"1.2.3.4\""),
        ];

        foreach ((object value, string canonical) in vectors)
        {
            HexalithIdempotencyHasher.Canonicalize(value).ShouldBe(canonical);
        }
    }
}
