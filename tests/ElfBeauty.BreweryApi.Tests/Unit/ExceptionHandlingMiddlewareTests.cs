using System.Text.Json;
using ElfBeauty.BreweryApi.Domain.Exceptions;
using ElfBeauty.BreweryApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElfBeauty.BreweryApi.Tests.Unit;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenRequestValidationExceptionIsThrown_WritesSingleValidProblemDetailsPayload()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new RequestValidationException("Page must be greater than or equal to 1."),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("Validation failed", root.GetProperty("title").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, root.GetProperty("status").GetInt32());
        Assert.Equal("Page must be greater than or equal to 1.", root.GetProperty("detail").GetString());
    }
}
