using System;

namespace BackendCodeChallengeApi.Data.Errors;

public abstract class DomainException(string message, Exception? innerException) : Exception(message, innerException);

public sealed class NotFoundException(string message, Exception? innerException) : DomainException(message, innerException);

public sealed class ConflictException(string message, Exception? innerException) : DomainException(message, innerException);

public sealed class BadRequestException(string message, Exception? innerException) : DomainException(message, innerException);
