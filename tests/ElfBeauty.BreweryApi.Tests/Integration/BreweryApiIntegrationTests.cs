using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ElfBeauty.BreweryApi.Tests.Integration;

public sealed class BreweryApiIntegrationTests(BreweryApiFactory factory) : IClassFixture<BreweryApiFactory>
{
    [Fact]
    public async Task GetBreweries_ReturnsSuccess()
    {
        using var client = factory.CreateClient();

        using var response =
            await client.GetAsync(
                "/api/v1/breweries?page=1&pageSize=10");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task DistanceWithoutCoordinates_ReturnsBadRequest()
    {
        using var client = factory.CreateClient();

        using var response =
            await client.GetAsync(
                "/api/v1/breweries?sortBy=distance");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task DistanceWithoutCoordinates_ReturnsFriendlyMessage()
    {
        using var client = factory.CreateClient();

        using var response =
            await client.GetAsync(
                "/api/v1/breweries?sortBy=distance");

        var content =
            await response.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "Latitude and longitude are required",
            content);
    }

    [Theory]
    [InlineData("name", "asc")]
    [InlineData("name", "desc")]
    [InlineData("city", "asc")]
    [InlineData("city", "desc")]
    public async Task Sorting_ReturnsSuccess(
        string sortBy,
        string direction)
    {
        using var client =
            factory.CreateClient();

        using var response =
            await client.GetAsync(
                $"/api/v1/breweries" +
                $"?sortBy={sortBy}" +
                $"&sortDirection={direction}" +
                "&page=1&pageSize=10");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Autocomplete_ReturnsSuccess()
    {
        using var client =
            factory.CreateClient();

        using var response =
            await client.GetAsync(
                "/api/v1/breweries/autocomplete" +
                "?term=brew&limit=10");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task InvalidPage_ReturnsBadRequest()
    {
        using var client =
            factory.CreateClient();

        using var response =
            await client.GetAsync(
                "/api/v1/breweries?page=0");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsSuccess()
    {
        using var client =
            factory.CreateClient();

        using var response =
            await client.GetAsync(
                "/health");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task
    Breweries_WhenMoreThan60RequestsAreMade_Returns429()
    {
        // Arrange
        await using var factory =
            new RateLimitedBreweryApiFactory();

        using var client =
            factory.CreateClient();

        const string requestUri =
            "/api/v1/breweries" +
            "?page=1&pageSize=10";

        // Act + Assert
        for (var requestNumber = 1; requestNumber <= 60; requestNumber++)
        {
            using var response = await client.GetAsync(requestUri);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // The 61st request exceeds the configured limit.
        using var rejectedResponse =
            await client.GetAsync(
                requestUri);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            rejectedResponse.StatusCode);

        Assert.Equal(
            "60",
            rejectedResponse.Headers
                .GetValues("Retry-After")
                .Single());

        Assert.Equal("application/json", rejectedResponse.Content.Headers.ContentType?.MediaType);

        Assert.True(rejectedResponse.Headers.Contains("Retry-After"));

        var responseBody = await rejectedResponse.Content.ReadAsStringAsync();

        Assert.Contains(
            "Too many requests",
            responseBody,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_IsNotSubjectToBreweryRateLimit()
    {
        using var client = factory.CreateClient();

        for (var index = 0; index < 5; index++)
        {
            using var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }


}