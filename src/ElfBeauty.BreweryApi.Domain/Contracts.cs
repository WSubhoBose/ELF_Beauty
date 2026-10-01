using ElfBeauty.BreweryApi.Domain.Models;
using ElfBeauty.BreweryApi.Infrastructure.Entities;

namespace ElfBeauty.BreweryApi.Domain
{   
    public sealed record PagedResponse<T>(IReadOnlyList<T> Breweries, int Page, int PageSize, int? TotalCount);

    public sealed record AutocompleteFilter(string QueryParameter, Func<SourceBrewery, string?> Selector);

    public sealed record BreweryWithDistance(BreweryResponse Brewery, double? DistanceKilometres);
}