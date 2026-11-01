using BackendCodeChallengeApi.Data.Errors;

namespace BackendCodeChallengeApi.Tests.Unit;

public sealed class SqlErrorTranslatorTests
{
    private const string Raw = "Violation of PRIMARY KEY constraint 'PK_Ship'. Cannot insert duplicate key (T01).";

    [Theory]
    [InlineData(50001, typeof(NotFoundException))]
    [InlineData(50002, typeof(NotFoundException))]
    [InlineData(50004, typeof(NotFoundException))]
    [InlineData(50003, typeof(ConflictException))]
    [InlineData(50010, typeof(BadRequestException))]
    public void Domain_codes_keep_their_message(int number, Type expected)
    {
        var result = SqlErrorTranslator.Translate(number, "sp message");

        Assert.IsType(expected, result);
        Assert.Equal("sp message", result!.Message);
    }

    [Theory]
    [InlineData(2627)]
    [InlineData(2601)]
    public void Unique_violations_become_generic_conflict(int number)
    {
        var result = SqlErrorTranslator.Translate(number, Raw);

        Assert.IsType<ConflictException>(result);
        Assert.Equal("The resource already exists.", result!.Message);
        Assert.DoesNotContain("PK_Ship", result.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50000)]
    [InlineData(547)]
    public void Unknown_number_returns_null(int number) =>
        Assert.Null(SqlErrorTranslator.Translate(number, "x"));

    [Fact]
    public void Inner_exception_is_preserved()
    {
        var inner = new InvalidOperationException("boom");

        Assert.Same(inner, SqlErrorTranslator.Translate(50001, "x", inner)!.InnerException);
        Assert.Same(inner, SqlErrorTranslator.Translate(2627, Raw, inner)!.InnerException);
    }
}