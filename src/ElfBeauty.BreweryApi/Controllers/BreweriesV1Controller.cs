using Asp.Versioning;
using ElfBeauty.BreweryApi.Domain;
using ElfBeauty.BreweryApi.Domain.Interfaces;
using ElfBeauty.BreweryApi.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElfBeauty.BreweryApi.Controllers
{
    [ApiController]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/breweries")]
    [AllowAnonymous]
    public sealed class BreweriesV1Controller(IBreweryService breweryDataService) : ControllerBase
    {
        /// <summary>Lists breweries with database-side filtering and pagination.</summary> 
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<BreweryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public Task<PagedResponse<BreweryResponse>> Get([FromQuery] BreweryQuery query, CancellationToken ct) => breweryDataService.GetAsync(query, ct);

        /// <summary>Returns brewery-name autocomplete suggestions.</summary> 
        [HttpGet("autocomplete")]
        [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public Task<IReadOnlyList<string>> Autocomplete([FromQuery] string term, [FromQuery] int limit = 10, CancellationToken ct = default) => breweryDataService.AutocompleteAsync(term, limit, ct);
    }
}