#region Licensing

// Copyright Stéphane Erard
// For licensing please contact <stephane.erard@gmail.com>

#endregion

#region Usings


#endregion

namespace FrenchExDev.Net.HttpClient;

/// <summary>
/// Defines a contract for performing HTTP GET requests and retrieving the response asynchronously.
/// </summary>
/// <remarks>Implementations of this interface can be used to abstract HTTP client functionality, enabling easier
/// testing and dependency injection. The returned response may contain status codes, headers, and content as provided
/// by the remote server.</remarks>
public interface IHttpClient
{
    /// <summary>
    /// Sends an asynchronous HTTP GET request to the specified URL.
    /// </summary>
    /// <remarks>The returned <see cref="HttpResponseMessage"/> must be disposed by the caller to release
    /// network resources. If the request is canceled, the returned task will be in the Canceled state.</remarks>
    /// <param name="url">The URL of the resource to request. Must be a valid absolute or relative URI.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the HTTP response message returned
    /// by the server.</returns>
    Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides functionality to perform HTTP requests using an underlying <see cref="HttpClient"/> instance.
/// </summary>
/// <remarks>This class is intended for scenarios where HTTP operations are required and allows for
/// cancellation via a <see cref="CancellationToken"/>. The lifetime and configuration of the underlying <see
/// cref="HttpClient"/> are managed externally and should be considered when using this class.</remarks>
public class HttpClient : IHttpClient
{
    /// <summary>
    /// Holds the http client
    /// </summary>
    private readonly System.Net.Http.HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the GetHttpClient class using the specified HttpClient.
    /// </summary>
    /// <param name="httpClient">The HttpClient instance to be used for sending HTTP requests. Cannot be null.</param>
    public HttpClient(System.Net.Http.HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Sends an asynchronous HTTP GET request to the specified URL.
    /// </summary>
    /// <remarks>The returned task completes when the entire response has been read. If the request is
    /// canceled via the cancellation token, the task will be faulted with an OperationCanceledException.</remarks>
    /// <param name="url">The URL of the resource to request. Must be a valid absolute URI.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the HTTP response message returned
    /// by the server.</returns>
    public Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        return _httpClient.GetAsync(url, cancellationToken);
    }
}
