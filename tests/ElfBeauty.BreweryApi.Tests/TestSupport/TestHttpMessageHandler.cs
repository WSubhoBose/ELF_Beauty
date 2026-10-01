namespace ElfBeauty.BreweryApi.Tests.TestSupport;

public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<
        HttpRequestMessage,
        HttpResponseMessage>
        responseFactory;

    public TestHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage>
            responseFactory)
    {
        this.responseFactory =
            responseFactory
            ?? throw new ArgumentNullException(
                nameof(responseFactory));
    }

    public List<HttpRequestMessage> Requests { get; } =
        [];

    public HttpRequestMessage? LastRequest =>
        Requests.LastOrDefault();

    protected override Task<HttpResponseMessage>
        SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        Requests.Add(request);

        return Task.FromResult(
            responseFactory(request));
    }
}