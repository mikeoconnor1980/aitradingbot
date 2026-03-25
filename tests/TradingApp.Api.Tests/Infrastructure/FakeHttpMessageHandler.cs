using System.Text;

namespace TradingApp.Api.Tests.Infrastructure;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string? _content;
    private readonly string? _mediaType;
    private readonly Exception? _exception;

    public FakeHttpMessageHandler(HttpResponseMessage response)
    {
        _statusCode = response.StatusCode;
        if (response.Content is not null)
        {
            _content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            _mediaType = response.Content.Headers.ContentType?.MediaType;
        }
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

        var response = new HttpResponseMessage(_statusCode)
        {
            RequestMessage = request,
        };

        if (_content is not null)
        {
            response.Content = new StringContent(_content, Encoding.UTF8, _mediaType ?? "application/json");
        }

        return Task.FromResult(response);
    }
}