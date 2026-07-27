using System.Runtime.CompilerServices;
using System.Web;
using Jobo.Enterprise.Client.Models;

namespace Jobo.Enterprise.Client;

/// <summary>
/// Sub-client for the Jobs Feed endpoints (POST /api/jobs/feed,
/// POST /api/jobs/feed/managed, GET /api/jobs/expired).
/// Access via <see cref="JoboClient.Feed"/>.
/// </summary>
public sealed class JobsFeedClient : JoboClientBase
{
    private const string FeedPath = "/api/jobs/feed";
    private const string ManagedFeedPath = "/api/jobs/feed/managed";

    private readonly TimeSpan? _feedTimeout;

    internal JobsFeedClient(HttpClient httpClient, TimeSpan? requestTimeout = null, TimeSpan? feedTimeout = null)
        : base(httpClient, requestTimeout)
    {
        _feedTimeout = feedTimeout;
    }

    /// <summary>
    /// Fetch a single batch of jobs from the feed.
    /// <para>
    /// When <see cref="JobFeedRequest.Cursor"/> is set the cursor is sent on its
    /// own — it already carries the filters and batch size from the first
    /// request, and anything sent beside it is ignored.
    /// </para>
    /// </summary>
    /// <param name="request">Feed request with filters, cursor, and batch size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="JobFeedResponse"/> with jobs, cursor, and pagination flag.</returns>
    public async Task<JobFeedResponse> GetJobsAsync(
        JobFeedRequest request,
        CancellationToken cancellationToken = default)
    {
        object body = string.IsNullOrEmpty(request.Cursor)
            ? WithoutCursor(request)
            : new CursorRequest { Cursor = request.Cursor };
        return await PostAsync<JobFeedResponse>(FeedPath, body, _feedTimeout, cancellationToken);
    }

    /// <summary>
    /// Enumerate all jobs from the feed, automatically handling cursor-based pagination.
    /// <para>The supplied request is not modified.</para>
    /// </summary>
    public async IAsyncEnumerable<Job> EnumerateJobsAsync(
        JobFeedRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetJobsAsync(WithoutCursor(request), cancellationToken);
        while (true)
        {
            foreach (var job in response.Jobs)
                yield return job;

            if (!response.HasMore || string.IsNullOrEmpty(response.NextCursor)) break;
            response = await GetJobsAsync(
                new JobFeedRequest { Cursor = response.NextCursor },
                cancellationToken);
        }
    }

    /// <summary>
    /// Fetch a single batch from the managed feed (POST /api/jobs/feed/managed).
    /// <para>
    /// Returns only jobs from companies configured through Managed Job Scraping
    /// in the Jobo portal. Same batch and cursor semantics as
    /// <see cref="GetJobsAsync"/>, with no locations filter. Throws
    /// <see cref="Exceptions.JoboPermissionException"/> for sandbox and
    /// marketplace keys, which carry no managed job sources.
    /// </para>
    /// </summary>
    public async Task<JobFeedResponse> GetManagedJobsAsync(
        ManagedJobFeedRequest request,
        CancellationToken cancellationToken = default)
    {
        object body = string.IsNullOrEmpty(request.Cursor)
            ? WithoutCursor(request)
            : new CursorRequest { Cursor = request.Cursor };
        return await PostAsync<JobFeedResponse>(ManagedFeedPath, body, _feedTimeout, cancellationToken);
    }

    /// <summary>
    /// Enumerate the whole managed feed, automatically handling pagination.
    /// <para>The supplied request is not modified.</para>
    /// </summary>
    public async IAsyncEnumerable<Job> EnumerateManagedJobsAsync(
        ManagedJobFeedRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetManagedJobsAsync(WithoutCursor(request), cancellationToken);
        while (true)
        {
            foreach (var job in response.Jobs)
                yield return job;

            if (!response.HasMore || string.IsNullOrEmpty(response.NextCursor)) break;
            response = await GetManagedJobsAsync(
                new ManagedJobFeedRequest { Cursor = response.NextCursor },
                cancellationToken);
        }
    }

    /// <summary>
    /// Fetch a single batch of expired job IDs.
    /// </summary>
    /// <param name="expiredSince">
    /// UTC timestamp. Optional — defaults to 24 hours ago server-side. Maximum
    /// lookback is 7 days.
    /// </param>
    /// <param name="cursor">Pagination cursor from a previous response.</param>
    /// <param name="batchSize">Number of IDs per batch (1–10000). Defaults to 1000.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ExpiredJobIdsResponse> GetExpiredJobIdsAsync(
        DateTime? expiredSince = null,
        string? cursor = null,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        if (expiredSince.HasValue)
            query["expired_since"] = expiredSince.Value.ToUniversalTime().ToString("O");
        query["batch_size"] = batchSize.ToString();
        if (!string.IsNullOrEmpty(cursor))
            query["cursor"] = cursor;

        return await GetAsync<ExpiredJobIdsResponse>($"/api/jobs/expired?{query}", cancellationToken);
    }

    /// <summary>
    /// Enumerate all expired job IDs, automatically handling cursor-based pagination.
    /// </summary>
    public async IAsyncEnumerable<Guid> EnumerateExpiredJobIdsAsync(
        DateTime? expiredSince = null,
        int batchSize = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? cursor = null;
        while (true)
        {
            var response = await GetExpiredJobIdsAsync(expiredSince, cursor, batchSize, cancellationToken);
            foreach (var id in response.JobIds)
                yield return id;

            if (!response.HasMore || string.IsNullOrEmpty(response.NextCursor)) break;
            cursor = response.NextCursor;
        }
    }

    private static JobFeedRequest WithoutCursor(JobFeedRequest request) => new()
    {
        Locations = request.Locations,
        Sources = request.Sources,
        WorkModels = request.WorkModels,
        EmploymentTypes = request.EmploymentTypes,
        ExperienceLevels = request.ExperienceLevels,
        PostedAfter = request.PostedAfter,
        UpdatedAfter = request.UpdatedAfter,
        StableScan = request.StableScan,
        BatchSize = request.BatchSize
    };

    private static ManagedJobFeedRequest WithoutCursor(ManagedJobFeedRequest request) => new()
    {
        Sources = request.Sources,
        WorkModels = request.WorkModels,
        PostedAfter = request.PostedAfter,
        UpdatedAfter = request.UpdatedAfter,
        BatchSize = request.BatchSize
    };

    /// <summary>A continuation request carries the cursor and nothing else.</summary>
    private sealed class CursorRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("cursor")]
        public string? Cursor { get; set; }
    }
}
