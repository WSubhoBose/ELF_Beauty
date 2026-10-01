using ElfBeauty.BreweryApi.Domain;
using ElfBeauty.BreweryApi.Domain.Helpers;
using ElfBeauty.BreweryApi.Domain.Interfaces;
using ElfBeauty.BreweryApi.Domain.Models;
using Microsoft.Extensions.Logging;

namespace ElfBeauty.BreweryApi.Application.Service
{
    public sealed class BreweryService(IBreweryCache breweryCache, IOpenBreweryClient openBreweryClient, ILogger<BreweryService> logger) : IBreweryService
    {
        public async Task<PagedResponse<BreweryResponse>> GetAsync(BreweryQuery query, CancellationToken cancellationToken)
        {
            BreweryHelper.Validate(query);

            var cacheKey = BreweryCacheKey.Create(query);

            if (breweryCache.TryGet<PagedResponse<BreweryResponse>>(cacheKey, out var cached) && cached is not null)
            {
                logger.LogDebug(
                    "Returning brewery query from cache. " +
                    "CacheKey: {CacheKey}",
                    cacheKey);

                return cached;
            }

            logger.LogInformation(
                "No Record found in Brewery query cache. " +
                "CacheKey: {CacheKey}",
                cacheKey);

            var response = await openBreweryClient.QueryAsync(query, cancellationToken);

            breweryCache.Set(cacheKey, response);

            return response;
        }

        public async Task<IReadOnlyList<string>> AutocompleteAsync(string term, int limit, CancellationToken cancellationToken)
        {
            BreweryHelper.ValidateAutocomplete(term, limit);
            var normalisedTerm = term.Trim();
            var cacheKey = BreweryCacheKey.CreateAutocomplete(normalisedTerm, limit);

            if (breweryCache.TryGet<IReadOnlyList<string>>(cacheKey, out var breweries) && breweries is not null)
            {
                logger.LogInformation("Brewery catalogue cache hit for autocomplete.");
                return breweries;
            }

            logger.LogDebug("Brewery catalogue cache miss for autocomplete.");

            var result = await openBreweryClient.AutocompleteAsync(normalisedTerm, limit, cancellationToken);
            breweryCache.Set(cacheKey, result);
            return result;
        }
    }
}
