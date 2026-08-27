using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ZEstate.Core.Exceptions;

namespace ZEstateApi.Filters;

// Lets services signal HTTP-mappable failures by throwing instead of every
// controller action having to know how to translate each service outcome.
public class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var statusCode = context.Exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            BadRequestException => StatusCodes.Status400BadRequest,
            ForbiddenException => StatusCodes.Status403Forbidden,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            _ => 0
        };

        if (statusCode == 0)
            return;

        context.Result = new ObjectResult(new { message = context.Exception.Message })
        {
            StatusCode = statusCode
        };
        context.ExceptionHandled = true;
    }
}
