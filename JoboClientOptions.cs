namespace Jobo.Enterprise.Client;

/// <summary>
/// Configuration options for <see cref="JoboClient"/>.
/// </summary>
public sealed class JoboClientOptions
{
    /// <summary>
    /// Your Jobo Enterprise API key. Required.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// API base URL. Defaults to <c>https://connect.jobo.world</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "https://connect.jobo.world";

    /// <summary>
    /// Request timeout. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Response timeout for the two feed endpoints, which stream up to 1,000
    /// full job records per call. Defaults to 120 seconds.
    /// </summary>
    public TimeSpan FeedTimeout { get; set; } = TimeSpan.FromSeconds(120);
}
