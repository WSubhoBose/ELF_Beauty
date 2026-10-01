using ElfBeauty.BreweryApi.Domain.Configurations;
using ElfBeauty.BreweryApi.Domain.Models;
using ElfBeauty.BreweryApi.Infrastructure;
using ElfBeauty.BreweryApi.Tests.TestData;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ElfBeauty.BreweryApi.Tests.Unit;

public sealed class BreweryMemoryCacheTests : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions());

    [Fact]
    public void Set_ThenTryGet_ReturnsStoredValue()
    {
        var sut =
            CreateSut();

        const string key =
            "breweries:test";

        var expected =
            BreweryTestData.CreateResponse(
                id: "1",
                name: "Alpha Brewing",
                city: "Denver");

        sut.Set(
            key,
            expected);

        var found =
            sut.TryGet<BreweryResponse>(
                key,
                out var actual);

        Assert.True(found);
        Assert.NotNull(actual);

        Assert.Same(
            expected,
            actual);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var sut =
            CreateSut();

        var found =
            sut.TryGet<BreweryResponse>(
                "breweries:missing",
                out var result);

        Assert.False(found);
        Assert.Null(result);
    }


    [Fact]
    public void Set_NullValue_Throws()
    {
        var sut = CreateSut();
        Assert.Throws<ArgumentNullException>(() => sut.Set<BreweryResponse>("breweries:test", null!));
    }

    private BreweryMemoryCache CreateSut()
    {
        var options =
            Options.Create(
                new CacheOptions
                {
                    BreweryExpirationMinutes = 10
                });

        return new BreweryMemoryCache(
            cache,
            options);
    }

    public void Dispose()
    {
        cache.Dispose();
    }

}