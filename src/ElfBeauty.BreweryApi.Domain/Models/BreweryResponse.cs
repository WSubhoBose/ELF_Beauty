namespace ElfBeauty.BreweryApi.Domain.Models
{
    public sealed record BreweryResponse(
    string Id,
    string Name,
    string BreweryType,
    string? Address1,
    string? Address2,
    string? Address3,
    string City,
    string StateProvince,
    string PostalCode,
    string Country,
    decimal? Longitude,
    decimal? Latitude,
    string? Phone,
    string? WebsiteUrl,
    string? State,
    string? Street,
    string? Distance = null);
}
