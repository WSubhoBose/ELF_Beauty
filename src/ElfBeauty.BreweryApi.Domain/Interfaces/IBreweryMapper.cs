using ElfBeauty.BreweryApi.Domain.Models;
using ElfBeauty.BreweryApi.Domain.Entities;

namespace ElfBeauty.BreweryApi.Domain.Interfaces;

public interface IBreweryMapper
{
    BreweryResponse Map(SourceBrewery source);
}
