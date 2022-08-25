using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ToDoList.Api.Middleware;

public class GlobalExceptionProblemDetailsFilter : IExceptionFilter, IFilterMetadata
{
    private readonly ILogger<GlobalExceptionProblemDetailsFilter> _logger;

    public GlobalExceptionProblemDetailsFilter(ILogger<GlobalExceptionProblemDetailsFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(
            new EventId(context.Exception.HResult), 
            context.Exception, 
            "Exception: {ExceptionMessage}, InnerException: {InnerExceptionMessage}",
            context.Exception?.Message,
            context.Exception?.InnerException?.Message
        );

        ProblemDetails exceptionDetails;

        context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        exceptionDetails = new ProblemDetails
        {
            Type = "server_error",
            Title = "Server error occured",
            Detail = context.Exception?.InnerException?.Message ?? context.Exception?.Message,
            Status = (int)HttpStatusCode.InternalServerError,
            Instance = context.HttpContext.Request?.Path
        };

        context.Result = new ObjectResult(exceptionDetails);
    }
}