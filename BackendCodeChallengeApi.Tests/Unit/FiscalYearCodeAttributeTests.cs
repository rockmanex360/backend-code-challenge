using BackendCodeChallengeApi.Contracts.Validation;

namespace BackendCodeChallengeApi.Tests.Unit;

public sealed class FiscalYearCodeAttributeTests
{
    private readonly FiscalYearCodeAttribute _attribute = new();

    [Theory]
    [InlineData("0112")]
    [InlineData("0403")]
    [InlineData("0706")]
    [InlineData("1211")]
    [InlineData("1110")]
    public void Valid_codes_pass(string code) => Assert.True(_attribute.IsValid(code));

    [Theory]
    [InlineData("0412")]
    [InlineData("1301")]
    [InlineData("0013")]
    [InlineData("0000")]
    [InlineData("00ab")]
    [InlineData("011")]
    [InlineData("01122")]
    [InlineData("")]
    [InlineData(" 112")]
    public void Invalid_codes_fail(string code) => Assert.False(_attribute.IsValid(code));

    [Fact]
    public void Null_is_left_to_Required() => Assert.True(_attribute.IsValid(null));
}