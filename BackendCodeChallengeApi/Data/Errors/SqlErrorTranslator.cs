using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;

namespace BackendCodeChallengeApi.Data.Errors;

public static class SqlErrorTranslator
{
    public static bool TryTranslate(SqlException exception, [NotNullWhen(true)] out DomainException? translated)
    {
        translated = exception.Number switch
        {
            50001 or 50002 => new NotFoundException(exception.Message, exception),
            50003 => new ConflictException(exception.Message, exception),
            2627 or 2601 => new ConflictException(exception.Message, exception),
            50010 => new BadRequestException(exception.Message, exception),
            _ => null
        };

        return translated is not null;
    }
}
