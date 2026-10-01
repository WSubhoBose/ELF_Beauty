using ElfBeauty.BreweryApi.Domain.Configurations;
using ElfBeauty.BreweryApi.Domain.Models;
using ElfBeauty.BreweryApi.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ElfBeauty.BreweryApi.Infrastructure
{
    public sealed class BreweryMemoryCache(IMemoryCache memoryCache, IOptions<CacheOptions> options) : IBreweryCache
    {
        private const string CacheKey = "brewery:catalogue:v1";

        public bool TryGet<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return memoryCache.TryGetValue(key, out value);
        }

        public void Set<T>(string key, T value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            ArgumentNullException.ThrowIfNull(value);

            memoryCache.Set(
            key,
            value,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.BreweryExpirationMinutes)
            });
        }
    }
}