namespace Jobo.Enterprise.Client;

/// <summary>
/// Client for the Jobo Enterprise API.
/// <para>
/// Access feature-specific sub-clients via properties:
/// <list type="bullet">
///   <item><see cref="Feed"/> — Bulk job feed with cursor-based pagination</item>
///   <item><see cref="Search"/> — Full-text job search with filters</item>
///   <item><see cref="Companies"/> — Enriched company profiles and per-company job listings</item>
///   <item><see cref="Locations"/> — Geocoding and location resolution</item>
/// </list>
/// </para>
/// Implements <see cref="IDisposable"/> to clean up the underlying <see cref="HttpClient"/>.
/// </summary>
public sealed class JoboClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Bulk job feed with cursor-based pagination.
    /// </summary>
    public JobsFeedClient Feed { get; }

    /// <summary>
    /// Full-text job search with filters and pagination.
    /// </summary>
    public JobsSearchClient Search { get; }

    /// <summary>
    /// Enriched company profiles and per-company job listings.
    /// </summary>
    public CompaniesClient Companies { get; }

    /// <summary>
    /// Geocoding and location resolution.
    /// </summary>
    public LocationsClient Locations { get; }

    /// <summary>
    /// Creates a new <see cref="JoboClient"/> with the specified options.
    /// </summary>
    public JoboClient(JoboClientOptions options)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl.TrimEnd('/')),
            // Timeouts are enforced per request instead, so the feed routes can
            // outlast the shorter default without raising it for everything.
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "jobo-dotnet/4.0.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _ownsHttpClient = true;

        Feed = new JobsFeedClient(_httpClient, options.Timeout, options.FeedTimeout);
        Search = new JobsSearchClient(_httpClient, options.Timeout);
        Companies = new CompaniesClient(_httpClient, options.Timeout);
        Locations = new LocationsClient(_httpClient, options.Timeout);
    }

    /// <summary>
    /// Creates a new <see cref="JoboClient"/> using an existing <see cref="HttpClient"/>.
    /// The caller is responsible for configuring headers, base address, and
    /// timeouts — note the feed endpoints want at least 120 seconds.
    /// </summary>
    public JoboClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ownsHttpClient = false;

        Feed = new JobsFeedClient(_httpClient);
        Search = new JobsSearchClient(_httpClient);
        Companies = new CompaniesClient(_httpClient);
        Locations = new LocationsClient(_httpClient);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
