using ElfBeauty.BreweryApi.Domain.Models;

namespace ElfBeauty.BreweryApi.Domain.Interfaces
{
    public interface IBreweryService
    {
        /// <summary>
        /// Retrieves breweries from the database when it is available and fresh.
        /// Falls back to Open Brewery DB when database connectivity is unavailable.
        /// </summary>
        Task<PagedResponse<BreweryResponse>> GetAsync(BreweryQuery query, CancellationToken cancellationToken);

        Task<IReadOnlyList<string>> AutocompleteAsync(string term, int limit, CancellationToken ct);
    }
}