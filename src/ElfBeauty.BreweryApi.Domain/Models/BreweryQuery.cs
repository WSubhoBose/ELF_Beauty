namespace ElfBeauty.BreweryApi.Domain.Models
{
    public sealed record BreweryQuery(
        string? Search = null,
        string? Name = null,
        string? City = null,
        string? SortBy = null,
        string SortDirection = "asc",
        int Page = 1,
        int PageSize = 50,
        decimal? Latitude = null,
        decimal? Longitude = null
    );
}