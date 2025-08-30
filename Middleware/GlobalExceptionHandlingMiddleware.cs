using System.Net;
using System.Text.Json;

namespace WSTKNG.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred for request {RequestPath} {RequestMethod}", 
                context.Request.Path, context.Request.Method);
            
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            error = "An internal server error occurred",
            requestId = context.TraceIdentifier
        };

        // Set appropriate status codes based on exception type
        switch (exception)
        {
            case ArgumentNullException:
            case ArgumentException:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response = new { error = "Invalid request parameters", requestId = context.TraceIdentifier };
                break;
            case UnauthorizedAccessException:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response = new { error = "Unauthorized access", requestId = context.TraceIdentifier };
                break;
            case FileNotFoundException:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                response = new { error = "Resource not found", requestId = context.TraceIdentifier };
                break;
            case TimeoutException:
                context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                response = new { error = "Request timeout", requestId = context.TraceIdentifier };
                break;
            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;
        }

        var jsonResponse = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(jsonResponse);
    }
}