using ZEstate.Core.Validation;

namespace ZEstate.Tests.Validation;

public class IbanValidatorTests
{
    [Theory]
    [InlineData("BG80BNBG96611020345678")] // official SWIFT registry example IBAN for Bulgaria
    [InlineData("GB29NWBK60161331926819")] // well-known UK example IBAN
    [InlineData("bg80bnbg96611020345678")] // lowercase
    [InlineData("BG80 BNBG 9661 1020 3456 78")] // spaced
    public void IsValid_KnownValidIbans_ReturnsTrue(string iban)
    {
        Assert.True(IbanValidator.IsValid(iban));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not an iban")]
    [InlineData("BG80BNBG96611020345679")] // last digit tampered - fails checksum
    [InlineData("BG80BNBG9661102034567")] // too short
    [InlineData("123456789012345678901234")] // no country code letters
    public void IsValid_InvalidIbans_ReturnsFalse(string? iban)
    {
        Assert.False(IbanValidator.IsValid(iban));
    }

    [Fact]
    public void Normalize_StripsSpacesAndUppercases()
    {
        Assert.Equal("BG80BNBG96611020345678", IbanValidator.Normalize("bg80 bnbg 9661 1020 3456 78"));
    }
}
