using System.Net;
using System.Text.Json;

namespace LiftLedger.Api.Middleware;

public class ExceptionMappingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMappingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionMappingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMappingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteAsync(context, ex);
        }
    }

    private async Task WriteAsync(HttpContext context, Exception exception)
    {
        var (status, title) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            AuthenticationFailedException => (HttpStatusCode.Unauthorized, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, exception.Message),
            ConflictException => (HttpStatusCode.Conflict, exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        if (status == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
        }

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        var payload = new
        {
            type = "about:blank",
            title,
            status = (int)status,
            detail = _environment.IsDevelopment() && status == HttpStatusCode.InternalServerError
                ? exception.ToString()
                : title
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}

public class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string message) : base(message)
    {
    }
}
