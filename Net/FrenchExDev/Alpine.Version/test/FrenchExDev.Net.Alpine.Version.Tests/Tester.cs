using FrenchExDev.Net.Builder;

namespace FrenchExDev.Net.Alpine.Version.Testing;

/// <summary>
/// Provides utility methods for testing the AlpineVersionSearcher with custom filters and assertions.
/// </summary>
/// <remarks>
/// This static class allows you to build search filters using <see cref="AlpineVersionSearchingFiltersBuilder"/>,
/// execute a search asynchronously, and assert the results using a provided delegate.
/// </remarks>
public static class AlpineVersionSearcherTester
{
    /// <summary>
    /// Runs an asynchronous test for AlpineVersionSearcher using the specified filter builder and assertion.
    /// </summary>
    /// <param name="filterBuilder">Action to configure the filter builder.</param>
    /// <param name="assert">Action to assert the search results.</param>
    /// <param name="cancellationToken">Optional cancellation token for the search operation.</param>
    public static async Task ValidAsync(
        IAlpineVersionSearcher searcher,
        Action<AlpineVersionSearchingFiltersBuilder> filterBuilder,
        Action<AlpineVersionList> assert,
        CancellationToken cancellationToken = default
    )
    {
        var result = await searcher.SearchAsync(filterBuilder, cancellationToken);
        assert(result);
    }

    /// <summary>
    /// Executes an asynchronous search for Alpine versions using the specified HTTP client and filter configuration, then
    /// applies an assertion to the search results.
    /// </summary>
    /// <param name="getHttpClientBuilder">A delegate that configures the HTTP client builder used for performing the search. This action should set up any
    /// required client behavior or dependencies.</param>
    /// <param name="filterBuilder">A delegate that configures the filters to apply when searching for Alpine versions. Use this action to specify
    /// search criteria.</param>
    /// <param name="assert">A delegate that performs assertions or validations on the resulting list of Alpine versions. This action is called
    /// after the search completes.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when the assertion delegate has been invoked
    /// on the search results.</returns>
    public static async Task ValidAsync(
        Action<FakeHttpClientBuilder> getHttpClientBuilder,
        Action<AlpineVersionSearchingFiltersBuilder> filterBuilder,
        Action<AlpineVersionList> assert,
        CancellationToken cancellationToken = default)
    {
        var builder = new FakeHttpClientBuilder();
        getHttpClientBuilder(builder);
        var buildResult = await builder.BuildAsync();
        var httpClient = buildResult.ValueOrThrow().Resolved();
        var result = await new AlpineVersionSearcher(httpClient).SearchAsync(filterBuilder, cancellationToken);
        assert(result);
    }
}
