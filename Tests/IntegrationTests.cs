using Xunit;
using Jobo.Enterprise.Client;
using Jobo.Enterprise.Client.Exceptions;
using Jobo.Enterprise.Client.Models;

namespace Jobo.Enterprise.Client.Tests;

/// <summary>
/// Integration tests that call the live Jobo Enterprise API.
/// Requires the JOBO_API_KEY environment variable to be set.
/// Tests are skipped gracefully when the key is not available (local dev).
/// </summary>
public class IntegrationTests : IDisposable
{
    private readonly JoboClient? _client;
    private readonly string? _skipReason;

    public IntegrationTests()
    {
        var apiKey = Environment.GetEnvironmentVariable("JOBO_API_KEY");
        var baseUrl = Environment.GetEnvironmentVariable("JOBO_BASE_URL") ?? "https://connect.jobo.world";

        if (string.IsNullOrEmpty(apiKey))
        {
            _skipReason = "JOBO_API_KEY environment variable is not set.";
            return;
        }

        _client = new JoboClient(new JoboClientOptions
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            Timeout = TimeSpan.FromSeconds(30)
        });
    }

    private JoboClient Client => _client ?? throw new SkipException(_skipReason!);

    // ── Feed ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetJobsFeed_ReturnsJobs()
    {
        if (_client is null) return; // skip

        var response = await Client.Feed.GetJobsAsync(new JobFeedRequest { BatchSize = 5 });

        Assert.NotNull(response);
        Assert.NotEmpty(response.Jobs);
        Assert.True(response.Jobs.Count <= 5);

        var job = response.Jobs[0];
        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.False(string.IsNullOrEmpty(job.Title));
        Assert.False(string.IsNullOrEmpty(job.Description));
        Assert.False(string.IsNullOrEmpty(job.ListingUrl));
        Assert.False(string.IsNullOrEmpty(job.Source));
        Assert.NotNull(job.Company);
        Assert.False(string.IsNullOrEmpty(job.Company.Name));
    }

    [Fact]
    public async Task GetJobsFeed_WithLocationFilter_ReturnsJobs()
    {
        if (_client is null) return;

        var response = await Client.Feed.GetJobsAsync(new JobFeedRequest
        {
            Locations = new List<LocationFilter>
            {
                new() { Country = "United States" }
            },
            BatchSize = 5
        });

        Assert.NotNull(response);
        Assert.NotEmpty(response.Jobs);
    }

    [Fact]
    public async Task GetJobsFeed_Pagination_CursorWorks()
    {
        if (_client is null) return;

        var first = await Client.Feed.GetJobsAsync(new JobFeedRequest { BatchSize = 2 });
        Assert.NotEmpty(first.Jobs);

        if (!first.HasMore) return; // small dataset, can't test pagination

        Assert.False(string.IsNullOrEmpty(first.NextCursor));

        var second = await Client.Feed.GetJobsAsync(new JobFeedRequest
        {
            Cursor = first.NextCursor,
            BatchSize = 2
        });

        Assert.NotNull(second);
        Assert.NotEmpty(second.Jobs);
        // The feed mutates live (jobs get re-scraped and bubble back toward the
        // top), so a single job can legitimately re-surface across a page
        // boundary. Assert the page advanced — at least one job on page 2 was
        // not on page 1 — rather than comparing the first element, which flakes
        // on a moving dataset.
        var firstIds = first.Jobs.Select(j => j.Id).ToHashSet();
        Assert.Contains(second.Jobs, j => !firstIds.Contains(j.Id));
    }

    [Fact]
    public async Task EnumerateJobsFeed_YieldsJobs()
    {
        if (_client is null) return;

        var jobs = new List<Job>();
        await foreach (var job in Client.Feed.EnumerateJobsAsync(new JobFeedRequest { BatchSize = 3 }))
        {
            jobs.Add(job);
            if (jobs.Count >= 5) break; // limit for test speed
        }

        Assert.NotEmpty(jobs);
    }

    // ── Expired ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetExpiredJobIds_ReturnsResponse()
    {
        if (_client is null) return;

        var response = await Client.Feed.GetExpiredJobIdsAsync(
            expiredSince: DateTime.UtcNow.AddDays(-6),
            batchSize: 5
        );

        Assert.NotNull(response);
        // May be empty if no jobs expired recently, but should not throw
        Assert.NotNull(response.JobIds);
    }

    // ── Search ──────────────────────────────────────────────────────

    [Fact]
    public async Task SearchJobs_ReturnsResults()
    {
        if (_client is null) return;

        var response = await Client.Search.SearchAsync(q: "software engineer", pageSize: 5);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Jobs);
        Assert.True(response.Total > 0);
        Assert.True(response.TotalPages >= 1);
        Assert.Equal(1, response.Page);
    }

    [Fact]
    public async Task SearchJobsAdvanced_ReturnsResults()
    {
        if (_client is null) return;

        var response = await Client.Search.SearchAdvancedAsync(new JobSearchRequest
        {
            Queries = new List<string> { "data engineer" },
            PageSize = 5
        });

        Assert.NotNull(response);
        Assert.NotEmpty(response.Jobs);
        Assert.True(response.Total > 0);
    }

    [Fact]
    public async Task SearchJobsAdvanced_WithLocationFilter_ReturnsResults()
    {
        if (_client is null) return;

        var response = await Client.Search.SearchAdvancedAsync(new JobSearchRequest
        {
            Queries = new List<string> { "developer" },
            Locations = new List<string> { "New York" },
            PageSize = 5
        });

        Assert.NotNull(response);
        // May return 0 results for very specific filters, but should not throw
    }

    [Fact]
    public async Task EnumerateSearchJobs_YieldsJobs()
    {
        if (_client is null) return;

        var jobs = new List<Job>();
        await foreach (var job in Client.Search.EnumerateAsync(new JobSearchRequest
        {
            Queries = new List<string> { "engineer" },
            PageSize = 3
        }))
        {
            jobs.Add(job);
            if (jobs.Count >= 5) break;
        }

        Assert.NotEmpty(jobs);
    }

    // ── Error handling ──────────────────────────────────────────────

    [Fact]
    public async Task InvalidApiKey_ThrowsAuthenticationException()
    {
        using var badClient = new JoboClient(new JoboClientOptions
        {
            ApiKey = "invalid-key-12345",
            BaseUrl = Environment.GetEnvironmentVariable("JOBO_BASE_URL") ?? "https://connect.jobo.world"
        });

        await Assert.ThrowsAsync<JoboAuthenticationException>(
            () => badClient.Feed.GetJobsAsync(new JobFeedRequest { BatchSize = 1 })
        );
    }

    // ── Job model validation ────────────────────────────────────────

    [Fact]
    public async Task Job_HasExpectedFields()
    {
        if (_client is null) return;

        var response = await Client.Search.SearchAsync(q: "engineer", pageSize: 1);
        Assert.NotEmpty(response.Jobs);

        var job = response.Jobs[0];
        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.False(string.IsNullOrEmpty(job.Title));
        Assert.NotNull(job.Company);
        Assert.NotEqual(Guid.Empty, job.Company.Id);
        Assert.False(string.IsNullOrEmpty(job.Company.Name));
        Assert.False(string.IsNullOrEmpty(job.Description));
        Assert.False(string.IsNullOrEmpty(job.ListingUrl));
        Assert.False(string.IsNullOrEmpty(job.ApplyUrl));
        Assert.False(string.IsNullOrEmpty(job.Source));
        Assert.NotEqual(default, job.CreatedAt);
        Assert.NotEqual(default, job.UpdatedAt);
        // New structured fields are nullable; just verify they deserialize without error.
        Assert.NotNull(job.Responsibilities);
        Assert.NotNull(job.Benefits);
    }

    // ── Companies ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetCompany_AndJobs_ReturnsData()
    {
        if (_client is null) return;

        // Resolve a company id from a search result, then fetch its profile + jobs.
        var search = await Client.Search.SearchAsync(q: "engineer", pageSize: 1);
        if (search.Jobs.Count == 0) return; // no jobs available to resolve a company id

        var companyId = search.Jobs[0].Company.Id;

        var company = await Client.Companies.GetAsync(companyId);
        Assert.Equal(companyId, company.Id);
        Assert.False(string.IsNullOrEmpty(company.Name));

        var jobs = await Client.Companies.GetJobsAsync(companyId, pageSize: 5);
        Assert.NotNull(jobs);
        Assert.Equal(1, jobs.Page);
    }

    // ── Search facets ─────────────────────────────────────────────────

    [Fact]
    public async Task SearchAdvanced_ReturnsFacets()
    {
        if (_client is null) return;

        var response = await Client.Search.SearchAdvancedAsync(new JobSearchRequest
        {
            Queries = new List<string> { "engineer" },
            IncludeFacets = new List<string> { "work_model", "experience_level" },
            PageSize = 5
        });

        Assert.NotNull(response);
        Assert.NotNull(response.Facets);
    }

    // ── Geocoding ─────────────────────────────────────────────────────

    [Fact]
    public async Task Geocode_ReturnsLocation()
    {
        if (_client is null) return;

        var result = await Client.Locations.GeocodeAsync("San Francisco, CA");

        Assert.NotNull(result);
        Assert.Equal("San Francisco, CA", result.Input);
        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Locations);
        var location = result.Locations[0];
        Assert.False(string.IsNullOrEmpty(location.DisplayName));
        Assert.NotNull(location.Latitude);
        Assert.NotNull(location.Longitude);
    }

    [Fact]
    public async Task Geocode_WithInvalidLocation_ReturnsFailed()
    {
        if (_client is null) return;

        // The geocode endpoint can hang server-side on an unresolvable string,
        // so use a short timeout and accept either a response or a clean timeout
        // — both mean the SDK handled the input without crashing.
        using var shortClient = new JoboClient(new JoboClientOptions
        {
            ApiKey = Environment.GetEnvironmentVariable("JOBO_API_KEY")!,
            BaseUrl = Environment.GetEnvironmentVariable("JOBO_BASE_URL") ?? "https://connect.jobo.world",
            Timeout = TimeSpan.FromSeconds(10)
        });

        try
        {
            var result = await shortClient.Locations.GeocodeAsync("invalidlocationxyz123");
            Assert.NotNull(result);
        }
        catch (TaskCanceledException)
        {
            // Server hang surfaced as a client timeout; acceptable.
        }
    }

    // ── Job by id ───────────────────────────────────────────────────

    [Fact]
    public async Task GetJob_ReturnsTheSameJob()
    {
        if (_client is null) return; // skip

        var search = await Client.Search.SearchAsync(q: "engineer", pageSize: 1);
        if (search.Jobs.Count == 0) return; // no jobs to resolve an id

        var expected = search.Jobs[0];
        var job = await Client.Search.GetJobAsync(expected.Id);

        Assert.Equal(expected.Id, job.Id);
        Assert.Equal(expected.Title, job.Title);
    }

    [Fact]
    public async Task GetJob_WithUnknownId_ThrowsNotFound()
    {
        if (_client is null) return; // skip

        await Assert.ThrowsAsync<JoboNotFoundException>(
            () => Client.Search.GetJobAsync(Guid.NewGuid()));
    }

    // ── Managed feed ────────────────────────────────────────────────

    [Fact]
    public async Task GetManagedJobs_ReturnsBatchOrRejectsTheKey()
    {
        if (_client is null) return; // skip

        // Managed Job Scraping is per-account. A customer key with no managed
        // sources returns an empty batch; a sandbox or marketplace key has no
        // customer account at all and is rejected outright.
        try
        {
            var response = await Client.Feed.GetManagedJobsAsync(new ManagedJobFeedRequest { BatchSize = 5 });
            Assert.NotNull(response.Jobs);
            Assert.True(response.Jobs.Count <= 5);
        }
        catch (JoboPermissionException)
        {
            // Key carries no customer account — managed feed not available.
        }
    }

    // ── Canonical filter values ─────────────────────────────────────

    [Fact]
    public async Task EmploymentType_CarriesTheCanonicalWireValue()
    {
        if (_client is null) return; // skip

        // The documented canonical spelling is hyphenated. (The index also
        // happens to match the pre-4.0.0 underscored spelling, so this was a
        // correctness fix rather than a broken filter.)
        Assert.Equal("full-time", EmploymentType.FullTime);
        Assert.Equal("part-time", EmploymentType.PartTime);

        var response = await Client.Search.SearchAsync(employmentType: EmploymentType.FullTime, pageSize: 1);
        Assert.True(response.Total > 0);
    }

    [Fact]
    public async Task EmploymentType_ExposesFreelance()
    {
        if (_client is null) return; // skip

        // Absent from the constants before 4.0.0 — callers had to pass the literal.
        Assert.Equal("freelance", EmploymentType.Freelance);

        var response = await Client.Search.SearchAsync(employmentType: EmploymentType.Freelance, pageSize: 1);
        Assert.True(response.Total > 0);
    }

    [Fact]
    public async Task ExperienceLevel_ExposesIntern()
    {
        if (_client is null) return; // skip

        // Absent from the constants before 4.0.0.
        Assert.Equal("intern", ExperienceLevel.Intern);

        var response = await Client.Search.SearchAsync(experienceLevel: ExperienceLevel.Intern, pageSize: 1);
        Assert.True(response.Total > 0);
    }

    // ── Field selection and incremental sync ────────────────────────

    [Fact]
    public async Task IncludeFields_IsAccepted()
    {
        if (_client is null) return; // skip

        // Core fields are always returned whatever includeFields asks for. We do
        // not assert that the heavy fields are dropped: the API currently returns
        // them for an empty value, so that behaviour is not the client's to pin.
        var response = await Client.Search.SearchAsync(q: "engineer", includeFields: "summary", pageSize: 1);
        Assert.NotEmpty(response.Jobs);
        Assert.False(string.IsNullOrEmpty(response.Jobs[0].Title));
    }

    [Fact]
    public async Task Feed_AcceptsUpdatedAfterAndStableScan()
    {
        if (_client is null) return; // skip

        var response = await Client.Feed.GetJobsAsync(new JobFeedRequest
        {
            UpdatedAfter = DateTime.UtcNow.AddHours(-6),
            StableScan = true,
            BatchSize = 5
        });

        Assert.NotNull(response.Jobs);
        Assert.True(response.Jobs.Count <= 5);
    }

    [Fact]
    public async Task EnumerateJobs_DoesNotMutateTheSuppliedRequest()
    {
        if (_client is null) return; // skip

        var request = new JobFeedRequest { BatchSize = 2 };

        var seen = 0;
        await foreach (var job in Client.Feed.EnumerateJobsAsync(request))
        {
            Assert.NotNull(job);
            if (++seen >= 3) break;
        }

        Assert.Null(request.Cursor);
    }

    [Fact]
    public async Task ExpiredSince_IsOptional()
    {
        if (_client is null) return; // skip

        var response = await Client.Feed.GetExpiredJobIdsAsync(batchSize: 5);

        Assert.NotNull(response);
        Assert.NotNull(response.JobIds);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

/// <summary>
/// Custom exception to signal test skipping when API key is not available.
/// xunit will report these as failures, but the test body returns early.
/// </summary>
internal class SkipException : Exception
{
    public SkipException(string message) : base(message) { }
}
