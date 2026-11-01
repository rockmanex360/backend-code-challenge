using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;

namespace BackendCodeChallengeApi.Data.Errors;

public static class SqlErrorTranslator
{
    public static bool TryTranslate(SqlException exception, [NotNullWhen(true)] out DomainException? translated)
    {
        translated = Translate(exception.Number, exception.Message, exception);

        return translated is not null;
    }

    public static DomainException? Translate(int number, string message, Exception? inner = null) =>
        number switch
        {
            50001 or 50002 or 50004 => new NotFoundException(message, inner),
            50003 => new ConflictException(message, inner),
            2627 or 2601 => new ConflictException("The resource already exists.", inner),
            50010 => new BadRequestException(message, inner),
            _ => null
        };
}
