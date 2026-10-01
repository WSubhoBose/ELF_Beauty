using System.Text.Json;
using ElfBeauty.BreweryApi.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace ElfBeauty.BreweryApi.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (RequestValidationException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status400BadRequest,
                "Validation failed",
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status400BadRequest,
                "Invalid request",
                "One or more request parameters are invalid.");
        }
        catch (JsonException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status502BadGateway,
                "Invalid external response",
                "Open Brewery DB returned an invalid response.");
        }
        catch (HttpRequestException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status503ServiceUnavailable,
                "External service unavailable",
                "Open Brewery DB is currently unavailable.");
        }
        catch (TimeoutException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status504GatewayTimeout,
                "External service timeout",
                "Open Brewery DB did not respond within " +
                "the expected time.");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request was cancelled. TraceId: {TraceId}", context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "An unexpected error occurred while " +
                "processing the request.");
        }
    }

    private async Task WriteProblemDetailsAsync(HttpContext context, Exception exception, int statusCode, string title, string detail)
    {
        if (context.Response.HasStarted)
        {
            logger.LogError(exception, "The response has already started. TraceId: {TraceId}", context.TraceIdentifier);

            throw exception;
        }

        LogException(context, exception, statusCode);

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problemDetails =
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(problemDetails, cancellationToken: context.RequestAborted);
    }

    private void LogException(HttpContext context, Exception exception, int statusCode)
    {
        if (statusCode >= 500)
        {
            logger.LogError(
                exception,
                "Request failed. " +
                "StatusCode: {StatusCode}, " +
                "Method: {Method}, " +
                "Path: {Path}, " +
                "TraceId: {TraceId}",
                statusCode,
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier
            );

            return;
        }

        logger.LogWarning(
            "Request rejected. " +
            "StatusCode: {StatusCode}, " +
            "Method: {Method}, " +
            "Path: {Path}, " +
            "Message: {ErrorMessage}, " +
            "TraceId: {TraceId}",
            statusCode,
            context.Request.Method,
            context.Request.Path,
            exception.Message,
            context.TraceIdentifier
        );
    }
}