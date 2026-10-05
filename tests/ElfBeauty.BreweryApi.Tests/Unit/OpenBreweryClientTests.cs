using System.Globalization;
using System.Net;
using System.Text;
using ElfBeauty.BreweryApi.Application.Mapping;
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
        "sort=type%2Cname%3Aasc")]
    [InlineData(
        "name",
        "desc",
        "sort=type%2Cname%3Adesc")]
    [InlineData(
        "city",
        "asc",
        "sort=type%2Ccity%3Aasc")]
    [InlineData(
        "city",
        "desc",
        "sort=type%2Ccity%3Adesc")]
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

    [Theory]
    [InlineData("asc", "Near Brewery", "Far Brewery")]
    [InlineData("desc", "Far Brewery", "Near Brewery")]
    public async Task QueryAsync_Distance_RespectsSortDirection(string sortDirection, string firstName, string secondName)
    {
        var handler = new TestHttpMessageHandler(_ =>
            CreateJsonResponse(
                """
                [
                  {
                    "id": "far",
                    "name": "Far Brewery",
                    "brewery_type": "micro",
                    "city": "Los Angeles",
                    "state_province": "California",
                    "postal_code": "90001",
                    "country": "United States",
                    "latitude": "34.0522",
                    "longitude": "-118.2437"
                  },
                  {
                    "id": "near",
                    "name": "Near Brewery",
                    "brewery_type": "micro",
                    "city": "San Diego",
                    "state_province": "California",
                    "postal_code": "92101",
                    "country": "United States",
                    "latitude": "32.7200",
                    "longitude": "-117.1600"
                  }
                ]
                """));

        var sut = CreateSut(handler);

        var result = await sut.QueryAsync(
            new BreweryQuery
            {
                SortBy = "distance",
                SortDirection = sortDirection,
                Latitude = 32.7157m,
                Longitude = -117.1611m,
                Page = 1,
                PageSize = 50
            },
            CancellationToken.None);

        Assert.Equal(firstName, result.Breweries[0].Name);
        Assert.Equal(secondName, result.Breweries[1].Name);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public async Task QueryAsync_Distance_MissingCoordinates_ReturnsNullDistance(string sortDirection)
    {
        var handler = new TestHttpMessageHandler(_ =>
            CreateJsonResponse(
                """
                [
                  {
                    "id": "near",
                    "name": "Near Brewery",
                    "brewery_type": "micro",
                    "city": "San Diego",
                    "state_province": "California",
                    "postal_code": "92101",
                    "country": "United States",
                    "latitude": "32.7200",
                    "longitude": "-117.1600"
                  },
                  {
                    "id": "missing",
                    "name": "No Coordinates",
                    "brewery_type": "brewpub",
                    "city": "Denver",
                    "state_province": "Colorado",
                    "postal_code": "80202",
                    "country": "United States",
                    "latitude": null,
                    "longitude": null
                  }
                ]
                """));

        var sut = CreateSut(handler);

        var result = await sut.QueryAsync(
            new BreweryQuery
            {
                SortBy = "distance",
                SortDirection = sortDirection,
                Latitude = 32.7157m,
                Longitude = -117.1611m,
                Page = 1,
                PageSize = 50
            },
            CancellationToken.None);

        Assert.Equal("Near Brewery", result.Breweries[0].Name);
        Assert.Equal("No Coordinates", result.Breweries[1].Name);
        Assert.NotNull(result.Breweries[0].Distance);
        Assert.Null(result.Breweries[1].Distance);
    }

    [Fact]
    public async Task QueryAsync_EncodesSpecialCharactersInQueryValues()
    {
        var handler = new TestHttpMessageHandler(_ =>
            CreateJsonResponse("[]"));

        var sut = CreateSut(handler);

        await sut.QueryAsync(
            new BreweryQuery
            {
                Name = "A/B & Co",
                City = "St. Louis",
                SortBy = "city",
                SortDirection = "desc",
                Page = 2,
                PageSize = 25
            },
            CancellationToken.None);

        var uri = handler.LastRequest!.RequestUri!.OriginalString;

        Assert.Contains("by_name=a%2Fb_%26_co", uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("by_city=st._louis", uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=type%2Ccity%3Adesc", uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("page=2", uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("per_page=25", uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_SearchOnly_UsesNameFilter()
    {
        var handler = new TestHttpMessageHandler(_ => CreateJsonResponse("[]"));
        var sut = CreateSut(handler);

        await sut.QueryAsync(
            new BreweryQuery
            {
                Search = "Craft Beer",
                Page = 1,
                PageSize = 10
            },
            CancellationToken.None);

        Assert.Contains("by_name=craft_beer", handler.LastRequest!.RequestUri!.OriginalString);
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
    public async Task AutocompleteAsync_MergesMatchingFieldsAndBreweryTypes()
    {
        var handler = new TestHttpMessageHandler(_ =>
            CreateJsonResponse(
                """
                [
                  {
                    "id": "1",
                    "name": "Micro House",
                    "brewery_type": "micro",
                    "city": "Miami",
                    "state_province": null,
                    "state": "Michigan",
                    "postal_code": "00000",
                    "country": "United States"
                  }
                ]
                """));
        var sut = CreateSut(handler);

        var result = await sut.AutocompleteAsync("mi", 4, CancellationToken.None);

        Assert.Equal(4, result.Count);
        Assert.Contains(result, value => value.Equals("Miami", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, value => value.Equals("Michigan", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, value => value.Equals("Micro House", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            handler.Requests,
            request => request.RequestUri!.OriginalString.Contains("by_type=micro", StringComparison.OrdinalIgnoreCase));
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
            new BreweryMapper();

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