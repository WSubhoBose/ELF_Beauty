# e.l.f. Beauty Brewery API

The e.l.f. Beauty Brewery API is a .NET 9 REST API backed by Open Brewery DB. It supports bounded listing, search, name and city filtering, name/city/distance ordering, pagination, logical multi-field autocomplete, query-specific in-memory caching, structured logging, rate limiting, resilient HTTP communication, API versioning, and RFC 7807-style error responses.

The API processes only the result set required for the current request. It does not download the complete brewery catalogue during a user request.

## Technology stack

- .NET 9 (`net9.0`) and C# 13
- ASP.NET Core Web API
- `Asp.Versioning` for URL-segment versioning
- Swashbuckle/OpenAPI
- Typed `HttpClient` through `IHttpClientFactory`
- Standard .NET HTTP resilience pipeline
- `IMemoryCache` for query-result caching
- ASP.NET Core global rate limiting
- Serilog and `ILogger<T>`
- xUnit, Moq, and `WebApplicationFactory<Program>`

## SDK selection

The repository uses a .NET 9 baseline while allowing later .NET 9 feature bands:

```json
{
  "sdk": {
    "version": "9.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

This allows compatible installed .NET 9 SDKs such as later 9.0 feature bands to build and run the solution.

## Architecture

The solution uses a layered architecture with separate API, Application, Domain, Infrastructure, and Test responsibilities.

```text
HTTP request
    ↓
ExceptionHandlingMiddleware
    ↓
Serilog request logging
    ↓
Global per-client rate limiting
    ├── permitted → continue
    └── rejected  → HTTP 429 Problem Details
    ↓
BreweriesV1Controller
    ↓
IBreweryService / BreweryService
    ├── BreweryHelper validation
    ├── BreweryCacheKey
    ├── IBreweryCache / BreweryMemoryCache
    └── IOpenBreweryClient / OpenBreweryClient
            ├── bounded external queries
            ├── SourceBrewery deserialisation
            ├── IBreweryMapper / BreweryMapper
            ├── logical multi-field autocomplete
            └── DistanceCalculator
                    ↓
              BreweryResponse
```

### Responsibility boundaries

- `BreweriesV1Controller` owns routing, model binding, and delegation for the v1 API surface.
- `BreweryService` validates requests, creates cache keys, checks the cache, calls the external client on a cache miss, and stores bounded responses.
- `BreweryMemoryCache` stores typed values by deterministic key with ten-minute absolute expiration.
- `OpenBreweryClient` builds bounded source requests, deserialises source DTOs, maps responses, performs multi-field autocomplete queries, and applies local distance calculation to returned distance-query results.
- `BreweryMapper` transforms `SourceBrewery` into `BreweryResponse`.
- `DistanceCalculator` calculates the distance between caller-supplied coordinates and brewery coordinates using the Haversine-style formula implemented in the domain helper.
- `ExceptionHandlingMiddleware` returns safe Problem Details responses and logs failures.
- Serilog records one structured request-completion event per request.

## Design patterns and techniques

The implementation uses eight principal patterns and techniques:

1. Layered Architecture
2. Dependency Injection
3. Dependency Inversion
4. Typed Client / Adapter
5. Query-specific Cache-Aside
6. Mapper
7. Options
8. Middleware

## API endpoints

### List, search, filter, sort, and paginate

```http
GET /api/v1/breweries
```

| Parameter | Description | Default |
|---|---|---|
| `search` | Brewery-name search | none |
| `name` | Brewery-name filter | none |
| `city` | City filter | none |
| `sortBy` | `name`, `city`, or `distance` | none |
| `sortDirection` | `asc` or `desc` | `asc` |
| `latitude` | Required for distance ordering | none |
| `longitude` | Required for distance ordering | none |
| `page` | Page number, minimum 1 | 1 |
| `pageSize` | Page size from 1 to 200 | 50 |

### Autocomplete

```http
GET /api/v1/breweries/autocomplete?term=den&limit=10
```

The public endpoint accepts only `term` and `limit`. The caller does not select a field.

The term is searched logically across supported fields in the code behind:

- brewery name;
- city;
- state/province;
- country;
- postal code;
- brewery type where applicable.

The client performs bounded source queries for supported Open Brewery DB filters, combines candidate values, removes duplicates case-insensitively, orders the values consistently, and applies the requested limit.

The response remains:

```csharp
Task<IReadOnlyList<string>>
```

Example:

```json
[
  "Denver",
  "Denver Beer Company"
]
```

`limit` must be between 1 and 20. Invalid values return HTTP 400 and are not silently clamped.

### Health

```http
GET /health
```

The health endpoint is excluded from the API request quota.

## API examples

```bash
BASE_URL="https://localhost:7001"
```

### List

```bash
curl -k "$BASE_URL/api/v1/breweries?page=1&pageSize=50"
```

### Search

```bash
curl -k "$BASE_URL/api/v1/breweries?search=brew&page=1&pageSize=25"
```

### City filter

```bash
curl -k "$BASE_URL/api/v1/breweries?city=San%20Diego&page=1&pageSize=25"
```

### Name ordering

```bash
curl -k "$BASE_URL/api/v1/breweries?sortBy=name&sortDirection=asc&page=1&pageSize=50"
```

### City ordering

```bash
curl -k "$BASE_URL/api/v1/breweries?sortBy=city&sortDirection=desc&page=1&pageSize=50"
```

### Distance ordering

```bash
curl -k "$BASE_URL/api/v1/breweries?sortBy=distance&latitude=32.7157&longitude=-117.1611&page=1&pageSize=50"
```

Open Brewery DB receives `by_dist=latitude,longitude` to select a bounded proximity-based result set. For every returned brewery with valid coordinates, the API independently calculates the distance between the input coordinates and the brewery coordinates using the Haversine formula.

```text
API latitude/longitude
        ↓
Haversine calculation
        ↑
brewery latitude/longitude
```

The bounded page is ordered from nearest to farthest. Breweries without usable coordinates are placed after breweries with valid coordinates.

The response includes a formatted distance:

```json
{
  "id": "near",
  "name": "Near Brewery",
  "latitude": 32.7200,
  "longitude": -117.1600,
  "distance": "0.49 km"
}
```

For a brewery without usable coordinates:

```json
{
  "distance": null
}
```

Numeric distance values are used internally for ordering. Formatting is applied only to the response value.

## Query-specific caching

The cache stores bounded API responses rather than the complete external catalogue.

```text
Request
    ↓
validate query
    ↓
build deterministic cache key
    ↓
cache lookup
    ├── hit  → return cached response
    └── miss → execute bounded external query
                 ↓
              map and enrich response
                 ↓
              cache for ten minutes
                 ↓
              return response
```

A brewery-query key includes all response-changing values: search, name, city, sort field, sort direction, latitude, longitude, page, and page size. Autocomplete uses a separate key containing the normalised term and limit.

Different parameters produce different entries. Repeating the same semantic request reuses its cached response.

```json
{
  "Cache": {
    "BreweryExpirationMinutes": 10
  }
}
```

`IMemoryCache` is local to one process. A scaled-out deployment that needs a shared cache would use a distributed implementation.

## External request construction

The external client sends bounded `page` and `per_page` values and supports source parameters such as:

```text
by_name=brew
by_city=San%20Diego
sort=type,name:asc
sort=type,name:desc
sort=type,city:asc
sort=type,city:desc
by_dist=32.7157,-117.1611
```

User-controlled values are trimmed and URL encoded so reserved characters remain part of one parameter value. Sort fields and directions come from validated values. Coordinates use invariant culture. Query parameters are emitted explicitly as `key=value` pairs.

If both `search` and `name` are supplied, the code applies `name` last and therefore gives it precedence in the final external query. This matches the current `BuildQueryParameters` implementation in the infrastructure client.

## Pagination metadata

`TotalCount` is populated only when an exact overall matching count is available. A bounded external response that does not provide an exact total returns `null`; the current page size is not reported as the global total.

## Mapping

```text
Open Brewery DB JSON
    ↓
SourceBrewery
    ↓
BreweryMapper
    ↓
BreweryResponse
```

Required response strings receive safe defaults when omitted by the source. Optional addresses, coordinates, phone, website, state, street, and calculated distance remain nullable.

## Validation and error handling

Known errors return RFC 7807-style Problem Details with:

```http
Content-Type: application/problem+json
```

Example:

```json
{
  "title": "Validation failed",
  "status": 400,
  "detail": "Latitude and longitude are required when sorting by distance.",
  "instance": "/api/v1/breweries",
  "traceId": "..."
}
```

The middleware distinguishes validation failures, malformed downstream JSON, unavailable external services, timeouts, cancellations, and unexpected failures. Problem Details bodies are serialised directly while preserving the `application/problem+json` media type.

## Rate limiting

The API uses a global fixed-window limiter for the controller pipeline. Requests share the same quota window across the API, and when the limit is exceeded the application responds with HTTP 429 Problem Details.

```json
{
  "RateLimiting": {
    "PermitLimit": 60,
    "WindowSeconds": 60,
    "QueueLimit": 0
  }
}
```

Boundary behaviour:

```text
Request 1  → permitted
...
Request 60 → permitted
Request 61 → HTTP 429 Too Many Requests
```

A rejected response includes:

- HTTP 429;
- `Retry-After`;
- `application/problem+json`;
- a safe Problem Details body with a trace ID.

The health endpoint is explicitly excluded from the quota.

## Logging

Serilog provides structured request and application logging. The pipeline contains one request-completion logger. Logged properties include method, path, status code, elapsed time, and trace ID.

Application logs cover query-cache hits and misses, external-query execution, rate-limit rejection, validation failures, and downstream failures. Generated log files are excluded from source control.

## HTTP resilience

The typed client uses timeout, retry with exponential backoff and jitter, and circuit-breaker behaviour. Strongly typed configuration is validated at startup. Retry settings are constrained to values supported by the resilience pipeline.

## API versioning and Swagger

The public contract is exposed as V1:

```text
/api/v1/breweries
```

Swagger endpoints:

```text
/swagger
/swagger/v1/swagger.json
```

## Configuration

```json
{
  "OpenBrewerySource": {
    "BaseUrl": "https://api.openbrewerydb.org/v1/breweries",
    "TimeoutSeconds": 30,
    "RetryCount": 3,
    "RetryDelaySeconds": 2,
    "CircuitBreaker": {
      "FailureRatio": 0.5,
      "MinimumThroughput": 5,
      "SamplingDurationSeconds": 30,
      "BreakDurationSeconds": 30
    }
  },
  "Cache": {
    "BreweryExpirationMinutes": 10
  },
  "RateLimiting": {
    "PermitLimit": 60,
    "WindowSeconds": 60,
    "QueueLimit": 0
  }
}
```

## Testing strategy

### Unit tests

- `BreweryServiceTests` verifies query-specific cache hits, bounded misses, distinct entries for different queries, and autocomplete caching.
- `BreweryCacheKeyTests` verifies key normalisation and inclusion of all response-changing parameters.
- `BreweryHelperTests` verifies request and autocomplete validation.
- `BreweryMapperTests` verifies source mapping and nullable behaviour.
- `BreweryMemoryCacheTests` verifies typed cache operations and guards.
- `HaversineDistanceCalculatorTests` verifies distance calculations using caller and brewery coordinates.
- `OpenBreweryClientTests` verifies bounded pagination, encoding, name/city sorting, `by_dist`, local nearest-first ordering, formatted kilometre output, missing-coordinate ordering, logical multi-field autocomplete, response mapping, and downstream failures.
- `ExceptionHandlingMiddlewareTests` verifies status mapping, safe details, trace IDs, and Problem Details media types.

### Integration tests

`WebApplicationFactory<Program>` exercises routing, middleware, controllers, validation, caching, autocomplete, sorting, health checks, and rate limiting. The external network boundary is replaced with `TestOpenBreweryClient` and controlled data.

Rate-limiting integration coverage verifies that requests 1 through 60 from the same caller are permitted and request 61 returns HTTP 429 with `Retry-After` and `application/problem+json`.

## Build and run

Prerequisites:

- .NET 9 SDK, version 9.0.100 or later within the .NET 9 SDK family;
- network access to Open Brewery DB for live execution.

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet run --project src/ElfBeauty.BreweryApi
```

Quality settings:

```xml
<TargetFramework>net9.0</TargetFramework>
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

## Scalability considerations

Every user request processes a bounded result set instead of downloading the complete external catalogue. Query-specific entries reduce cold-request latency and bound per-entry memory use.

For larger deployments:

- use a distributed cache across API instances;
- cache selected hot queries and bounded results;
- enforce shared quotas at an API gateway or distributed limiter;
- monitor cache hit ratio, latency, evictions, retries, request rejection, and circuit-breaker state;
- use background incremental ingestion only when local durable storage is justified;
- prefer stable cursor/keyset pagination when supported.
