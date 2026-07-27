using System.Web;
using Jobo.Enterprise.Client.Models;

namespace Jobo.Enterprise.Client;

/// <summary>
/// Sub-client for the Locations/Geocoding endpoints (GET /api/locations/geocode).
/// Access via <see cref="JoboClient.Locations"/>.
/// </summary>
public sealed class LocationsClient : JoboClientBase
{
    internal LocationsClient(HttpClient httpClient, TimeSpan? requestTimeout = null)
        : base(httpClient, requestTimeout) { }

    /// <summary>
    /// Geocode a location string into structured locations with coordinates.
    /// </summary>
    /// <param name="location">The location string to geocode (e.g., "San Francisco, CA" or "London, UK").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GeocodeResultItem"/> with resolved locations.</returns>
    public async Task<GeocodeResultItem> GeocodeAsync(
        string location,
        CancellationToken cancellationToken = default)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["location"] = location;

        return await GetAsync<GeocodeResultItem>($"/api/locations/geocode?{query}", cancellationToken);
    }
}
