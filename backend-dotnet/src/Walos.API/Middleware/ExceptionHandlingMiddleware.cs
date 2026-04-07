using System.Text.Json;
using Walos.Application.DTOs.Common;
using Walos.Domain.Exceptions;

namespace Walos.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            BusinessException => StatusCodes.Status422UnprocessableEntity,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        _logger.LogError(exception, "Error capturado: {Name} - {Message} - URL: {Url} - Method: {Method}",
            exception.GetType().Name, exception.Message, context.Request.Path, context.Request.Method);

        var response = new Dictionary<string, object?>
        {
            ["success"] = false,
            ["message"] = exception.Message
        };

        if (exception is BusinessException bizEx && bizEx.Code is not null)
            response["code"] = bizEx.Code;

        if (exception is ValidationException valEx && valEx.Details is not null)
            response["details"] = valEx.Details;

        if (_env.IsDevelopment())
        {
            response["stack"] = exception.StackTrace;
            response["error"] = exception.GetType().Name;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
