using System.Runtime.CompilerServices;
using System.Web;
using Jobo.Enterprise.Client.Models;

namespace Jobo.Enterprise.Client;

/// <summary>
/// Sub-client for the Jobs Search endpoints (GET /api/jobs, POST /api/jobs/search,
/// GET /api/jobs/{id}).
/// Access via <see cref="JoboClient.Search"/>.
/// </summary>
public sealed class JobsSearchClient : JoboClientBase
{
    internal JobsSearchClient(HttpClient httpClient, TimeSpan? requestTimeout = null)
        : base(httpClient, requestTimeout) { }

    /// <summary>
    /// Fetch a single job by ID (GET /api/jobs/{id}).
    /// <para>
    /// Unmetered — this endpoint deducts no wallet credits, though it still
    /// counts toward the per-key request rate limit. Throws
    /// <see cref="Exceptions.JoboNotFoundException"/> when no job has that ID.
    /// </para>
    /// </summary>
    /// <param name="jobId">The Jobo job ID, as returned on every search and feed job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Job> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<Job>($"/api/jobs/{jobId}", cancellationToken);
    }

    /// <summary>
    /// Search jobs using simple query parameters (GET /api/jobs).
    /// </summary>
    /// <param name="q">
    /// Free-text search query. Wrap it in double quotes for an exact, contiguous
    /// phrase match against the job title only.
    /// </param>
    /// <param name="searchDescription">
    /// When true (the server default) match title, alternative titles, company,
    /// popular skills and summary. When false match titles and curated
    /// alternative titles only.
    /// </param>
    /// <param name="location">Location string filter.</param>
    /// <param name="sources">Comma-separated source identifiers.</param>
    /// <param name="workModel">Comma-separated work models ("remote", "hybrid", "onsite").</param>
    /// <param name="employmentType">Comma-separated employment types ("full-time", "part-time", ...).</param>
    /// <param name="experienceLevel">Comma-separated experience levels ("intern", "entry", ...).</param>
    /// <param name="postedAfter">Only jobs whose employer posting date is at or after this.</param>
    /// <param name="postedBefore">Only jobs whose employer posting date is at or before this.</param>
    /// <param name="discoveredAfter">Only jobs first indexed at or after this UTC timestamp.</param>
    /// <param name="discoveredBefore">Only jobs first indexed at or before this UTC timestamp.</param>
    /// <param name="minSalaryUsd">Minimum salary (USD) filter.</param>
    /// <param name="maxSalaryUsd">Maximum salary (USD) filter.</param>
    /// <param name="skills">Comma-separated required skills.</param>
    /// <param name="industries">Comma-separated company industries.</param>
    /// <param name="includeFacets">Comma-separated facets to compute. Pass "" to skip facets entirely.</param>
    /// <param name="includeFields">
    /// Comma-separated heavy fields to keep — description, summary,
    /// qualifications, responsibilities, benefits. Null returns the whole job;
    /// pass "" for core fields only.
    /// </param>
    /// <param name="page">Page number (1-indexed).</param>
    /// <param name="pageSize">Results per page (1–100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<JobSearchResponse> SearchAsync(
        string? q = null,
        bool? searchDescription = null,
        string? location = null,
        string? sources = null,
        string? workModel = null,
        string? employmentType = null,
        string? experienceLevel = null,
        DateTime? postedAfter = null,
        DateTime? postedBefore = null,
        DateTime? discoveredAfter = null,
        DateTime? discoveredBefore = null,
        int? minSalaryUsd = null,
        int? maxSalaryUsd = null,
        string? skills = null,
        string? industries = null,
        string? includeFacets = null,
        string? includeFields = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrEmpty(q)) query["q"] = q;
        if (searchDescription.HasValue)
            query["search_description"] = searchDescription.Value ? "true" : "false";
        if (!string.IsNullOrEmpty(location)) query["location"] = location;
        if (!string.IsNullOrEmpty(sources)) query["sources"] = sources;
        if (!string.IsNullOrEmpty(workModel)) query["work_model"] = workModel;
        if (!string.IsNullOrEmpty(employmentType)) query["employment_type"] = employmentType;
        if (!string.IsNullOrEmpty(experienceLevel)) query["experience_level"] = experienceLevel;
        if (postedAfter.HasValue) query["posted_after"] = postedAfter.Value.ToUniversalTime().ToString("O");
        if (postedBefore.HasValue) query["posted_before"] = postedBefore.Value.ToUniversalTime().ToString("O");
        if (discoveredAfter.HasValue)
            query["discovered_after"] = discoveredAfter.Value.ToUniversalTime().ToString("O");
        if (discoveredBefore.HasValue)
            query["discovered_before"] = discoveredBefore.Value.ToUniversalTime().ToString("O");
        if (minSalaryUsd.HasValue) query["min_salary_usd"] = minSalaryUsd.Value.ToString();
        if (maxSalaryUsd.HasValue) query["max_salary_usd"] = maxSalaryUsd.Value.ToString();
        if (!string.IsNullOrEmpty(skills)) query["skills"] = skills;
        if (!string.IsNullOrEmpty(industries)) query["industries"] = industries;
        // Empty string is meaningful on both of these: it asks for no facets /
        // core fields only. Only null means "leave it out and take the default".
        if (includeFacets is not null) query["include_facets"] = includeFacets;
        if (includeFields is not null) query["include_fields"] = includeFields;
        query["page"] = page.ToString();
        query["page_size"] = pageSize.ToString();

        return await GetAsync<JobSearchResponse>($"/api/jobs?{query}", cancellationToken);
    }

    /// <summary>
    /// Search jobs using the advanced body-based endpoint (POST /api/jobs/search).
    /// </summary>
    public async Task<JobSearchResponse> SearchAdvancedAsync(
        JobSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostAsync<JobSearchResponse>("/api/jobs/search", request, cancellationToken);
    }

    /// <summary>
    /// Enumerate all search results, automatically handling page-based pagination.
    /// Facets are disabled while paginating.
    /// <para>The supplied request is not modified.</para>
    /// </summary>
    public async IAsyncEnumerable<Job> EnumerateAsync(
        JobSearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var page = 1;
        while (true)
        {
            var pageRequest = ForPage(request, page);
            var response = await SearchAdvancedAsync(pageRequest, cancellationToken);
            foreach (var job in response.Jobs)
                yield return job;

            if (page >= response.TotalPages) break;
            page++;
        }
    }

    private static JobSearchRequest ForPage(JobSearchRequest request, int page) => new()
    {
        Queries = request.Queries,
        SearchDescription = request.SearchDescription,
        Locations = request.Locations,
        Sources = request.Sources,
        Skills = request.Skills,
        Companies = request.Companies,
        Industries = request.Industries,
        WorkModels = request.WorkModels,
        EmploymentTypes = request.EmploymentTypes,
        ExperienceLevels = request.ExperienceLevels,
        SalaryUsd = request.SalaryUsd,
        PostedAfter = request.PostedAfter,
        PostedBefore = request.PostedBefore,
        DiscoveredAfter = request.DiscoveredAfter,
        DiscoveredBefore = request.DiscoveredBefore,
        IncludeFacets = new List<string>(),
        IncludeFields = request.IncludeFields,
        Page = page,
        PageSize = request.PageSize
    };
}
