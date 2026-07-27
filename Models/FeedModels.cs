using System.Text.Json.Serialization;

namespace Jobo.Enterprise.Client.Models;

/// <summary>
/// Structured location filter for the feed endpoint.
/// </summary>
public sealed class LocationFilter
{
    [JsonPropertyName("country")] public string? Country { get; set; }
    [JsonPropertyName("region")] public string? Region { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
}

/// <summary>
/// Request body for the jobs feed endpoint (POST /api/jobs/feed).
/// <para>
/// The cursor preserves the filters and batch size from the first request, so a
/// continuation carries the cursor alone; start a new scan to change them.
/// </para>
/// </summary>
public sealed class JobFeedRequest
{
    [JsonPropertyName("locations")] public List<LocationFilter>? Locations { get; set; }
    [JsonPropertyName("sources")] public List<string>? Sources { get; set; }
    [JsonPropertyName("work_models")] public List<string>? WorkModels { get; set; }
    [JsonPropertyName("employment_types")] public List<string>? EmploymentTypes { get; set; }
    [JsonPropertyName("experience_levels")] public List<string>? ExperienceLevels { get; set; }
    [JsonPropertyName("posted_after")] public DateTime? PostedAfter { get; set; }

    /// <summary>Jobs created or updated at or after this — the incremental-sync watermark.</summary>
    [JsonPropertyName("updated_after")] public DateTime? UpdatedAfter { get; set; }

    /// <summary>
    /// Page by immutable creation time. Defaults to <c>true</c> server-side; set
    /// <c>false</c> for the legacy update-recency ordering.
    /// </summary>
    [JsonPropertyName("stable_scan")] public bool? StableScan { get; set; }

    [JsonPropertyName("cursor")] public string? Cursor { get; set; }
    [JsonPropertyName("batch_size")] public int BatchSize { get; set; } = 1000;
}

/// <summary>
/// Request body for the managed jobs feed (POST /api/jobs/feed/managed).
/// <para>
/// Same shape as <see cref="JobFeedRequest"/> minus the <c>Locations</c>
/// filter, which the managed endpoint does not support.
/// </para>
/// </summary>
public sealed class ManagedJobFeedRequest
{
    [JsonPropertyName("sources")] public List<string>? Sources { get; set; }
    [JsonPropertyName("work_models")] public List<string>? WorkModels { get; set; }
    [JsonPropertyName("posted_after")] public DateTime? PostedAfter { get; set; }
    [JsonPropertyName("updated_after")] public DateTime? UpdatedAfter { get; set; }
    [JsonPropertyName("cursor")] public string? Cursor { get; set; }
    [JsonPropertyName("batch_size")] public int BatchSize { get; set; } = 1000;
}

/// <summary>
/// Response from the jobs feed endpoints.
/// </summary>
public sealed class JobFeedResponse
{
    [JsonPropertyName("jobs")] public List<Job> Jobs { get; set; } = new();
    [JsonPropertyName("next_cursor")] public string? NextCursor { get; set; }
    [JsonPropertyName("has_more")] public bool HasMore { get; set; }

    /// <summary>Estimated size of the scan. Returned on the first page only.</summary>
    [JsonPropertyName("estimated_total")] public long? EstimatedTotal { get; set; }
}

/// <summary>
/// Response from the expired job IDs endpoint.
/// </summary>
public sealed class ExpiredJobIdsResponse
{
    [JsonPropertyName("job_ids")] public List<Guid> JobIds { get; set; } = new();
    [JsonPropertyName("next_cursor")] public string? NextCursor { get; set; }
    [JsonPropertyName("has_more")] public bool HasMore { get; set; }
}
