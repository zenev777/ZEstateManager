namespace ZEstate.Core.Exceptions;

// Thrown by service-layer methods to signal an HTTP-mappable failure without
// taking a dependency on ASP.NET Core. ZEstateApi.Filters.ApiExceptionFilter
// translates each of these into the corresponding status code + { message } body.

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message)
    {
    }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "Нямаш достъп.") : base(message)
    {
    }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
