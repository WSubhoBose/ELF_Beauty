using ElfBeauty.BreweryApi.Domain.Interfaces;
using ElfBeauty.BreweryApi.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElfBeauty.BreweryApi.Tests.Integration;

public sealed class RateLimitedBreweryApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RateLimiting:PermitLimit"] =
                            "2",

                        ["RateLimiting:WindowSeconds"] =
                            "60",

                        ["RateLimiting:QueueLimit"] =
                            "0",

                        ["Cache:BreweryExpirationMinutes"] =
                            "10",

                        ["OpenBrewerySource:BaseUrl"] =
                            "https://test.openbrewery.local/",

                        ["OpenBrewerySource:TimeoutSeconds"] =
                            "30",

                        ["OpenBrewerySource:RetryCount"] =
                            "1"
                    });
            });

        builder.ConfigureTestServices(
            services =>
            {
                services.RemoveAll<
                    IOpenBreweryClient>();

                services.AddSingleton<
                    IOpenBreweryClient,
                    TestOpenBreweryClient>();
            });
    }
}