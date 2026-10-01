using System.Globalization;
using System.Net;
using System.Text;
using ElfBeauty.BreweryApi.Domain.Helpers;
using ElfBeauty.BreweryApi.Domain.Interfaces;
using ElfBeauty.BreweryApi.Domain.Models;
using ElfBeauty.BreweryApi.Infrastructure;
using ElfBeauty.BreweryApi.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ElfBeauty.BreweryApi.Tests.Unit;

public sealed class OpenBreweryClientTests
{
    [Theory]
    [InlineData(
        "name",
        "asc",
        "sort=type,name:asc")]
    [InlineData(
        "name",
        "desc",
        "sort=type,name:desc")]
    [InlineData(
        "city",
        "asc",
        "sort=type,city:asc")]
    [InlineData(
        "city",
        "desc",
        "sort=type,city:desc")]
    public async Task QueryAsync_Sort_BuildsExpectedUri(
        string sortBy,
        string direction,
        string expected)
    {
        var handler =
            SuccessHandler();

        var sut =
            CreateSut(
                handler);

        await sut.QueryAsync(
            new BreweryQuery
            {
                SortBy = sortBy,
                SortDirection = direction,
                Page = 1,
                PageSize = 50
            },
            CancellationToken.None);

        var uri =
            handler.LastRequest?
                .RequestUri?
                .OriginalString;

        Assert.NotNull(uri);

        Assert.Contains(
            expected,
            uri);

        Assert.DoesNotContain(
            "%253A",
            uri,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_Distance_UsesByDist()
    {
        var handler =
            SuccessHandler();

        var sut =
            CreateSut(
                handler);

        await sut.QueryAsync(
            new BreweryQuery
            {
                SortBy = "distance",
                Latitude = 32.7157m,
                Longitude = -117.1611m,
                Page = 1,
                PageSize = 50
            },
            CancellationToken.None);

        var uri =
            Uri.UnescapeDataString(
                handler.LastRequest!
                    .RequestUri!
                    .OriginalString);

        Assert.Contains(
            "by_dist=32.7157,-117.1611",
            uri);

        Assert.DoesNotContain(
            "sort=",
            uri);
    }

    [Fact]
    public async Task QueryAsync_IncludesPagination()
    {
        var handler =
            SuccessHandler();

        var sut =
            CreateSut(
                handler);

        await sut.QueryAsync(
            new BreweryQuery
            {
                Page = 3,
                PageSize = 25
            },
            CancellationToken.None);

        var uri =
            handler.LastRequest!
                .RequestUri!
                .OriginalString;

        Assert.Contains(
            "page=3",
            uri);

        Assert.Contains(
            "per_page=25",
            uri);
    }

    [Fact]
    public async Task QueryAsync_ExternalFailure_Throws()
    {
        var handler =
            new TestHttpMessageHandler(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.ServiceUnavailable));

        var sut =
            CreateSut(
                handler);

        await Assert.ThrowsAsync<
            HttpRequestException>(
            () =>
                sut.QueryAsync(
                    new BreweryQuery
                    {
                        Page = 1,
                        PageSize = 50
                    },
                    CancellationToken.None));
    }

    [Fact]
    public async Task AutocompleteAsync_SearchesAcrossSupportedFields()
    {
        var handler =
            new TestHttpMessageHandler(
                request =>
                {
                    var uri =
                        request.RequestUri!
                            .OriginalString;

                    if (uri.Contains(
                            "by_name=den",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return CreateJsonResponse(
                            """
                        [
                          {
                            "id": "1",
                            "name": "Denver Beer Company",
                            "brewery_type": "micro",
                            "city": "Denver",
                            "state_province": "Colorado",
                            "postal_code": "80202",
                            "country": "United States"
                          }
                        ]
                        """);
                    }

                    if (uri.Contains(
                            "by_city=den",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return CreateJsonResponse(
                            """
                        [
                          {
                            "id": "2",
                            "name": "Test Brewery",
                            "brewery_type": "brewpub",
                            "city": "Denver",
                            "state_province": "Colorado",
                            "postal_code": "80203",
                            "country": "United States"
                          }
                        ]
                        """);
                    }

                    return CreateJsonResponse(
                        "[]");
                });

        var sut =
            CreateSut(handler);

        var result =
            await sut.AutocompleteAsync(
                "den",
                10,
                CancellationToken.None);

        Assert.NotNull(result);

        Assert.Contains(
            "Denver",
            result);

        Assert.Contains(
            "Denver Beer Company",
            result);
    }

    [Fact]
    public async Task AutocompleteAsync_QueriesSupportedFields()
    {
        // Arrange
        var handler =
            new TestHttpMessageHandler(
                _ => CreateJsonResponse("[]"));

        var sut =
            CreateSut(handler);

        // Act
        await sut.AutocompleteAsync(
            "den",
            10,
            CancellationToken.None);

        // Assert
        var requests =
            handler.Requests
                .Select(
                    request =>
                        Uri.UnescapeDataString(
                            request.RequestUri!
                                .OriginalString))
                .ToList();

        Assert.Contains(
            requests,
            uri =>
                uri.Contains(
                    "by_name=den",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            requests,
            uri =>
                uri.Contains(
                    "by_city=den",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            requests,
            uri =>
                uri.Contains(
                    "by_state=den",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            requests,
            uri =>
                uri.Contains(
                    "by_country=den",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            requests,
            uri =>
                uri.Contains(
                    "by_postal=den",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CalculateKilometres_UsesApiAndBreweryCoordinates()
    {
        var sut = new DistanceCalculator();

        // API input: San Diego
        const decimal inputLatitude =
            32.7157m;

        const decimal inputLongitude =
            -117.1611m;

        // Nearby brewery
        var nearDistance =
            sut.CalculateKilometres(
                inputLatitude,
                inputLongitude,
                32.7200m,
                -117.1600m);

        // Brewery around Los Angeles
        var farDistance =
            sut.CalculateKilometres(
                inputLatitude,
                inputLongitude,
                34.0522m,
                -118.2437m);

        Assert.True(
            nearDistance <
            farDistance);
    }

    private static TestHttpMessageHandler SuccessHandler()
    {
        return new TestHttpMessageHandler(
            _ =>
                new HttpResponseMessage(
                    HttpStatusCode.OK)
                {
                    Content =
                        new StringContent(
                            "[]",
                            Encoding.UTF8,
                            "application/json")
                });
    }

private static double ParseDistanceKilometres(string distance)
{
    var numericValue =
        distance.Replace(
            " km",
            string.Empty,
            StringComparison.OrdinalIgnoreCase);

    return double.Parse(
        numericValue,
        CultureInfo.InvariantCulture);
}

    private static OpenBreweryClient CreateSut(HttpMessageHandler handler, IBreweryMapper? mapper = null)
    {
        var httpClient =
            new HttpClient(handler)
            {
                BaseAddress =
                    new Uri(
                        "https://api.example.test/")
            };

        mapper ??=
            Mock.Of<IBreweryMapper>();

        var logger =
            Mock.Of<
                ILogger<OpenBreweryClient>>();

        return new OpenBreweryClient(httpClient, mapper, logger);
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
        };
    }
}