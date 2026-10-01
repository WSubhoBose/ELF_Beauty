using ElfBeauty.BreweryApi.Domain.Interfaces;
using ElfBeauty.BreweryApi.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElfBeauty.BreweryApi.Tests.Integration;

public sealed class BreweryApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(
            services =>
            {
                services.RemoveAll<IOpenBreweryClient>();
                services.AddSingleton<IOpenBreweryClient, TestOpenBreweryClient>();
            });
    }
}