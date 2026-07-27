using System.Web;
using Jobo.Enterprise.Client.Models;

namespace Jobo.Enterprise.Client;

/// <summary>
/// Sub-client for the Companies endpoints (GET /api/companies/{id}, GET /api/companies/{id}/jobs).
/// Access via <see cref="JoboClient.Companies"/>.
/// </summary>
public sealed class CompaniesClient : JoboClientBase
{
    internal CompaniesClient(HttpClient httpClient, TimeSpan? requestTimeout = null)
        : base(httpClient, requestTimeout) { }

    /// <summary>
    /// Fetch a fully enriched company profile (GET /api/companies/{id}).
    /// This endpoint is public — no API key required.
    /// </summary>
    /// <param name="companyId">The Jobo company id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Company"/> with the full enriched profile.</returns>
    public async Task<Company> GetAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<Company>($"/api/companies/{companyId}", cancellationToken);
    }

    /// <summary>
    /// List jobs that belong to a specific company (GET /api/companies/{id}/jobs).
    /// </summary>
    /// <param name="companyId">The Jobo company id.</param>
    /// <param name="postedAfter">Only jobs posted after this UTC timestamp.</param>
    /// <param name="page">Page number (1-indexed).</param>
    /// <param name="pageSize">Results per page (1–100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="JobSearchResponse"/> scoped to this company.</returns>
    public async Task<JobSearchResponse> GetJobsAsync(
        Guid companyId,
        DateTime? postedAfter = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        if (postedAfter.HasValue) query["posted_after"] = postedAfter.Value.ToUniversalTime().ToString("O");
        query["page"] = page.ToString();
        query["page_size"] = pageSize.ToString();

        return await GetAsync<JobSearchResponse>($"/api/companies/{companyId}/jobs?{query}", cancellationToken);
    }
}
