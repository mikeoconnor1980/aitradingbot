namespace TradingApp.Api.Tests.Infrastructure;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage? _response;
    private readonly Exception? _exception;

    public FakeHttpMessageHandler(HttpResponseMessage response)
    {
        _response = response;
    }

    public FakeHttpMessageHandler(Exception exception)
    {
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_exception is not null)
        {
            throw _exception;
        }

        return Task.FromResult(_response!);
    }
}