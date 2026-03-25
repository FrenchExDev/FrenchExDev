using FrenchExDev.Net.Builder;
using FrenchExDev.Net.Result;
using System.Net;

namespace FrenchExDev.Net.HttpClient.Testing;

/// <summary>
/// Provides a builder for creating and configuring instances of <see cref="System.Net.Http.HttpResponseMessage"/> using
/// a fluent interface.
/// </summary>
/// <remarks>Use <see cref="HttpResponseMessageBuilder"/> to construct <see cref="HttpResponseMessage"/> objects
/// with custom status codes and content for testing or programmatic response generation. This builder supports method
/// chaining for setting properties before building the final response message.</remarks>
public class HttpResponseMessageBuilder : IBuilder<HttpResponseMessage>
{
    public Guid Id => throw new NotImplementedException();

    public IResult? Result => throw new NotImplementedException();

    private HttpStatusCode _httpStatusCode;

    public HttpResponseMessageBuilder StatusCode(HttpStatusCode httpStatusCode)
    {
        _httpStatusCode = httpStatusCode;
        return this;
    }

    public HttpResponseMessageBuilder Ok()
    {
        return StatusCode(HttpStatusCode.OK);
    }

    private string? _content;
    public HttpResponseMessageBuilder Content(string content)
    {
        _content = content;
        return this;
    }
    public Task<Result<Reference<HttpResponseMessage>>> BuildAsync(VisitedObjects? visitedCollector = null, CancellationToken cancellationToken = default)
    {
        var r = new HttpResponseMessage(_httpStatusCode)
        {
            Content = new StringContent(_content ?? throw new InvalidOperationException(nameof(_content)))
        };

        return Task.FromResult<Result<Reference<HttpResponseMessage>>>(Result<Reference<HttpResponseMessage>>.Success(new Reference<HttpResponseMessage>().Resolve(r)));
    }

    public void OnBuilt(Action<HttpResponseMessage> hook)
    {
        throw new NotImplementedException();
    }

    public Reference<HttpResponseMessage> Reference()
    {
        throw new NotImplementedException();
    }
}
