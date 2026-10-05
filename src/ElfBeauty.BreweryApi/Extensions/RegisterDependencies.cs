using ElfBeauty.BreweryApi.Application.Service;
using ElfBeauty.BreweryApi.Domain.Configurations;
using ElfBeauty.BreweryApi.Domain.Interfaces;
using ElfBeauty.BreweryApi.Infrastructure;
using ElfBeauty.BreweryApi.Application.Mapping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Polly;
using System.Globalization;
using System.Threading.RateLimiting;

namespace ElfBeauty.BreweryApi.Extensions
{
    /// <summary>Registers the brewery API's application and infrastructure dependencies.</summary>
    public static class RegisterDependencies
    {
        /// <summary>
        /// Registers persistence and external-service infrastructure.
        /// </summary>
        public static IServiceCollection AddDependencies(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<OpenBrewerySourceOptions>()
                    .Bind(configuration.GetSection(OpenBrewerySourceOptions.SectionName))
                    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "OpenBreweryDb:BaseUrl must be a valid absolute URL.")
                    .Validate(options => options.TimeoutSeconds > 0, "OpenBreweryDb:TimeoutSeconds must be greater than zero.")
                    .ValidateOnStart();

            services.AddOptions<CacheOptions>()
                    .Bind(configuration.GetSection(CacheOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

            services.AddOptions<RateLimitOptions>()
                    .Bind(configuration.GetSection(RateLimitOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

            var rateLimitOptions = configuration.GetSection(RateLimitOptions.SectionName)
                    .Get<RateLimitOptions>()
                    ?? throw new InvalidOperationException("Rate-limiting configuration is missing.");

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddFixedWindowLimiter(
                    policyName: "BreweryApiPolicy",
                    limiterOptions =>
                    {
                        limiterOptions.PermitLimit = rateLimitOptions.PermitLimit;
                        limiterOptions.Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds);
                        limiterOptions.QueueLimit = rateLimitOptions.QueueLimit;
                        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                        limiterOptions.AutoReplenishment = true;
                    });

                options.OnRejected =
                    async (context, cancellationToken) =>
                    {
                        var httpContext = context.HttpContext;
                        var response = httpContext.Response;
                        response.StatusCode = StatusCodes.Status429TooManyRequests;

                        response.Headers.RetryAfter =
                            rateLimitOptions
                                .WindowSeconds
                                .ToString(CultureInfo.InvariantCulture);

                        var problemDetails =
                            new ProblemDetails
                            {
                                Status = StatusCodes.Status429TooManyRequests,
                                Title = "Too many requests",

                                Detail =
                                    $"The API request limit of " +
                                    $"{rateLimitOptions.PermitLimit} requests " +
                                    $"per {rateLimitOptions.WindowSeconds} seconds " +
                                    "has been exceeded.",

                                Instance = httpContext.Request.Path
                            };

                        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

                        await response.WriteAsJsonAsync(problemDetails,
                            cancellationToken: cancellationToken);
                    };
            });

            services.AddHealthChecks();

            var openBrewerySettings = configuration.GetSection(OpenBrewerySourceOptions.SectionName)
                            .Get<OpenBrewerySourceOptions>()
                            ?? throw new InvalidOperationException("OpenBrewery Source configuration is missing.");

            services.AddHttpClient<IOpenBreweryClient, OpenBreweryClient>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<OpenBrewerySourceOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            }).AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = openBrewerySettings.RetryCount;
                options.Retry.Delay = TimeSpan.FromSeconds(openBrewerySettings.RetryDelaySeconds);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.CircuitBreaker.FailureRatio = openBrewerySettings.CircuitBreaker.FailureRatio;
                options.CircuitBreaker.MinimumThroughput = openBrewerySettings.CircuitBreaker.MinimumThroughput;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(openBrewerySettings.CircuitBreaker.SamplingDurationSeconds);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(openBrewerySettings.CircuitBreaker.BreakDurationSeconds);
            });

            services.AddScoped<IBreweryMapper, BreweryMapper>();
            services.AddMemoryCache();
            services.AddSingleton<IBreweryCache, BreweryMemoryCache>();
            services.AddScoped<IBreweryService, BreweryService>();

            return services;
        }
    }
}
