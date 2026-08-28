using System.Numerics;
using System.Text.RegularExpressions;

namespace ZEstate.Core.Validation;

// Standard ISO 13616 / MOD-97-10 IBAN checksum validation - format + check digits,
// not whether the account actually exists.
public static class IbanValidator
{
    private static readonly Regex Pattern = new("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");

    public static bool IsValid(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return false;

        var cleaned = iban.Replace(" ", "").ToUpperInvariant();
        if (!Pattern.IsMatch(cleaned))
            return false;

        var rearranged = cleaned[4..] + cleaned[..4];
        var numeric = new System.Text.StringBuilder(rearranged.Length * 2);
        foreach (var c in rearranged)
        {
            numeric.Append(char.IsDigit(c) ? c.ToString() : (c - 'A' + 10).ToString());
        }

        return BigInteger.Parse(numeric.ToString()) % 97 == 1;
    }

    // Normalizes for storage: uppercase, no spaces. Caller should validate first.
    public static string Normalize(string iban) => iban.Replace(" ", "").ToUpperInvariant();
}
