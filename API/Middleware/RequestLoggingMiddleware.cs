using System.Diagnostics;

namespace API.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        catch
        {
            stopwatch.Stop();
            logger.LogInformation("{Method} {Path} {StatusCode} {Elapsed}ms",
                context.Request.Method, context.Request.Path,
                StatusCodes.Status500InternalServerError, stopwatch.ElapsedMilliseconds);
            throw;
        }

        stopwatch.Stop();
        logger.LogInformation("{Method} {Path} {StatusCode} {Elapsed}ms",
            context.Request.Method, context.Request.Path,
            context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    }
}
