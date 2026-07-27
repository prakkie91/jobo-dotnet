<img src="https://raw.githubusercontent.com/Prakkie91/jobo-dotnet/main/jobo-logo.png" alt="Jobo" width="120" />

# Jobo Enterprise — .NET Client

**Access millions of job listings, enriched company profiles, and geocoding — all from a single API.**

[![NuGet](https://img.shields.io/nuget/v/Jobo.Enterprise.Client)](https://www.nuget.org/packages/Jobo.Enterprise.Client)
[![.NET](https://img.shields.io/badge/.NET-6.0%20%7C%208.0-blue)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## Features

| Sub-client          | Property           | Description                                              |
| ------------------- | ------------------ | -------------------------------------------------------- |
| **Jobs Feed**       | `client.Feed`      | Bulk and managed job feeds with cursor-based pagination (106 ATS) |
| **Jobs Search**     | `client.Search`    | Full-text search, filters, facets, and single-job lookup |
| **Companies**       | `client.Companies` | Enriched company profiles and per-company job listings   |
| **Locations**       | `client.Locations` | Geocode location strings into structured coordinates     |

> **Get your API key** → [enterprise.jobo.world/api-keys](https://enterprise.jobo.world/api-keys)

---

## Installation

```bash
dotnet add package Jobo.Enterprise.Client
```

## Quick Start

```csharp
using Jobo.Enterprise.Client;
using Jobo.Enterprise.Client.Models;

using var client = new JoboClient(new JoboClientOptions { ApiKey = "your-api-key" });

// Search for jobs
var results = await client.Search.SearchAsync(q: "software engineer", location: "San Francisco");
foreach (var job in results.Jobs)
    Console.WriteLine($"{job.Title} at {job.Company.Name}");

// Geocode a location
var geo = await client.Locations.GeocodeAsync("London, UK");
Console.WriteLine($"{geo.Locations[0].DisplayName}: {geo.Locations[0].Latitude}, {geo.Locations[0].Longitude}");
```

## Authentication

```csharp
var client = new JoboClient(new JoboClientOptions { ApiKey = "your-api-key" });
```

## Dependency Injection

```csharp
using Jobo.Enterprise.Client.Extensions;

builder.Services.AddJoboClient(options =>
{
    options.ApiKey = builder.Configuration["Jobo:ApiKey"]!;
    options.Timeout = TimeSpan.FromSeconds(60);
});
```

Then inject and use sub-clients:

```csharp
public class MyService(JoboClient jobo)
{
    public async Task DoWorkAsync()
    {
        var results = await jobo.Search.SearchAsync(q: "engineer");
        var geo = await jobo.Locations.GeocodeAsync("Berlin, DE");
    }
}
```

---

## Jobs Feed — `client.Feed`

Bulk-sync millions of active jobs using cursor-based pagination.

### Fetch a batch

```csharp
var response = await client.Feed.GetJobsAsync(new JobFeedRequest
{
    Locations = [
        new LocationFilter { Country = "US", Region = "California" },
        new LocationFilter { Country = "US", City = "New York" }
    ],
    Sources = ["greenhouse", "workday"],
    WorkModels = ["remote", "hybrid"],
    BatchSize = 1000
});

Console.WriteLine($"Got {response.Jobs.Count} jobs, HasMore={response.HasMore}");
```

### Auto-paginate all jobs

```csharp
await foreach (var job in client.Feed.EnumerateJobsAsync(new JobFeedRequest
{
    Sources = ["greenhouse"],
    BatchSize = 1000
}))
{
    await SaveToDatabaseAsync(job);
}
```

### Incremental sync

After the initial backfill, set `UpdatedAfter` to pick up only what changed.
Scans page by immutable creation time by default (`StableScan`), so records
cannot shift across page boundaries while you are reading.

```csharp
await foreach (var job in client.Feed.EnumerateJobsAsync(new JobFeedRequest
{
    UpdatedAfter = DateTime.UtcNow.AddHours(-1),
    BatchSize = 1000
}))
{
    await UpsertAsync(job);
}
```

### Managed feed

Jobs from the companies you configured through **Managed Job Scraping** in the
Jobo portal. Same batch and cursor semantics, minus the locations filter.

```csharp
await foreach (var job in client.Feed.EnumerateManagedJobsAsync(new ManagedJobFeedRequest
{
    BatchSize = 1000
}))
{
    await SaveToDatabaseAsync(job);
}
```

### Expired job IDs

The timestamp is optional and defaults to 24 hours ago. Maximum lookback is 7 days.

```csharp
await foreach (var jobId in client.Feed.EnumerateExpiredJobIdsAsync())
{
    await MarkAsExpiredAsync(jobId);
}
```

---

## Jobs Search — `client.Search`

Full-text search with filters and page-based pagination.

### Simple search

```csharp
var results = await client.Search.SearchAsync(
    q: "data scientist",
    location: "New York",
    sources: "greenhouse,lever",
    workModel: WorkModel.Remote, // or just "remote"
    minSalaryUsd: 120000,
    pageSize: 50
);

Console.WriteLine($"Found {results.Total} jobs across {results.TotalPages} pages");
```

> **Closed value sets.** Parameters with a fixed set of accepted values expose
> their known values as `const string` fields for discoverability — `WorkModel`,
> `EmploymentType`, `ExperienceLevel`, `CompensationPeriod`, and `SkillType`.
> The methods still take `string`, so passing the literal (e.g. `"remote"`) is
> always valid too. Values are lowercase and hyphenated (`"full-time"`,
> `"per-diem"`); the API matches them exactly, so a misspelt value simply
> matches nothing.

### Fetch one job

```csharp
var job = await client.Search.GetJobAsync(jobId);
```

Unmetered — this endpoint deducts no credits, which makes it a cheap way to wire
up an integration.

### Trim the payload

Leave `includeFields` null for the whole job, pass a subset to keep only those
heavy fields, or pass `""` for core fields only.

```csharp
var results = await client.Search.SearchAsync(q: "data scientist", includeFields: "summary", pageSize: 50);
```

### Advanced search (multiple queries, filters & facets)

```csharp
var results = await client.Search.SearchAdvancedAsync(new JobSearchRequest
{
    Queries = ["machine learning engineer", "ML engineer", "AI engineer"],
    Locations = ["San Francisco", "New York"],
    Sources = ["greenhouse", "lever", "ashby"],
    WorkModels = ["remote", "hybrid"],
    Skills = new InclusionExclusionFilter { Include = ["python", "pytorch"], Exclude = ["php"] },
    SalaryUsd = new RangeFilter { Min = 150000 },
    IncludeFacets = ["source", "experience_level"],
    PageSize = 100
});

// Inspect aggregated facet counts
foreach (var (facet, buckets) in results.Facets)
foreach (var bucket in buckets)
    Console.WriteLine($"{facet}: {bucket.Key} ({bucket.Count})");
```

### Auto-paginate all results

```csharp
await foreach (var job in client.Search.EnumerateAsync(new JobSearchRequest
{
    Queries = ["backend engineer"],
    Locations = ["London"],
    PageSize = 100
}))
{
    Console.WriteLine($"{job.Title} — {job.Company.Name}");
}
```

---

## Companies — `client.Companies`

Fetch enriched company profiles and list jobs scoped to a single company.

```csharp
// Full enriched profile (this endpoint is public — no API key required)
var company = await client.Companies.GetAsync(companyId);
Console.WriteLine($"{company.Name} — {company.Website}");

// Jobs for that company, newest first
var jobs = await client.Companies.GetJobsAsync(companyId, page: 1, pageSize: 25);
foreach (var job in jobs.Jobs)
    Console.WriteLine($"{job.Title} ({job.WorkplaceType})");
```

---

## Locations — `client.Locations`

Geocode location strings into structured data with coordinates.

```csharp
var result = await client.Locations.GeocodeAsync("San Francisco, CA");

foreach (var location in result.Locations)
    Console.WriteLine($"{location.DisplayName}: {location.Latitude}, {location.Longitude}");
```

---

## Auto Apply

Not covered by this client. The Auto Apply contract is profileless and
callback-driven, and application creation is not yet open to traffic. Call it
over plain HTTPS — see the
[Auto Apply reference](https://jobo.world/docs/api-reference/auto-apply/auto-apply).

---

## Error Handling

`429` and `503` are retried for you with bounded backoff, honouring
`Retry-After`. Everything else throws immediately, as a subclass of
`JoboException`:

```csharp
using Jobo.Enterprise.Client.Exceptions;

try
{
    var results = await client.Search.SearchAsync(q: "engineer");
}
catch (JoboAuthenticationException)
{
    Console.WriteLine("Invalid API key");
}
catch (JoboPermissionException)
{
    Console.WriteLine("Key is not entitled to this resource");
}
catch (JoboNotFoundException)
{
    Console.WriteLine("No such job or company");
}
catch (JoboRateLimitException ex)
{
    Console.WriteLine($"Rate limited. Retry after {ex.RetryAfterSeconds}s");
}
catch (JoboValidationException ex)
{
    Console.WriteLine($"Bad request: {ex.Detail} ({ex.Code})");
}
catch (JoboCursorRestartRequiredException)
{
    Console.WriteLine("Feed cursor is spent — discard it and start a new scan");
}
catch (JoboServerException)
{
    Console.WriteLine("Server error — try again later");
}
```

Every exception carries the API's machine-readable problem `Code` when one is
supplied, alongside `StatusCode`, `Detail`, and the raw `ResponseBody`.

## Supported ATS Sources (106)

| Category           | Sources                                                                                                                                       |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------- |
| **Enterprise ATS** | `workday`, `smartrecruiters`, `icims`, `successfactors`, `oraclecloud`, `taleo`, `dayforce`, `csod`, `adp`, `ultipro`, `paycom`               |
| **Tech & Startup** | `greenhouse`, `lever_co`, `ashby`, `workable`, `workable_jobs`, `rippling`, `polymer`, `gem`, `pinpoint`, `homerun`                           |
| **Mid-Market**     | `bamboohr`, `breezy`, `jazzhr`, `recruitee`, `personio`, `jobvite`, `teamtailor`, `comeet`, `trakstar`, `zoho`                                |
| **SMB & Niche**    | `gohire`, `recooty`, `applicantpro`, `hiringthing`, `careerplug`, `hirehive`, `kula`, `careerpuck`, `talnet`, `jobscore`                      |
| **Specialized**    | `freshteam`, `isolved`, `joincom`, `eightfold`, `phenompeople`                                                                                |

The full catalogue of 106 providers is listed in the
[API documentation](https://jobo.world/docs/sources). Treat it as an open set —
new `provider_id` values appear as platforms are added.

## Configuration

| Property      | Default                      | Description                          |
| ------------- | ---------------------------- | ------------------------------------ |
| `ApiKey`      | _required_                   | Your API key                         |
| `BaseUrl`     | `https://connect.jobo.world` | API base URL                         |
| `Timeout`     | `00:00:30`                   | Request timeout                      |
| `FeedTimeout` | `00:02:00`                   | Response timeout for the feed routes |

## Custom HttpClient

```csharp
var httpClient = new HttpClient { BaseAddress = new Uri("https://connect.jobo.world") };
httpClient.DefaultRequestHeaders.Add("X-Api-Key", "your-api-key");

var client = new JoboClient(httpClient);
// client.Feed, client.Search, client.Companies and client.Locations are all available
```

When you supply the `HttpClient`, its `Timeout` governs every request — set it
to at least 120 seconds if you use the feed endpoints.

## Use Cases

- **Build a job board** — Search and display jobs from 106 ATS platforms
- **Job aggregator** — Bulk-sync millions of listings with the feed endpoint
- **ATS data pipeline** — Pull jobs from Greenhouse, Lever, Workday, etc. into your data warehouse
- **Recruitment tools** — Power candidate-facing job search experiences
- **Company intelligence** — Enrich listings with funding, headcount, and tech-stack data
- **Location intelligence** — Geocode and normalize job locations

## Target Frameworks

- .NET 8.0
- .NET 6.0

## Links

- **Website** — [jobo.world/enterprise](https://jobo.world/enterprise/)
- **Get API Key** — [enterprise.jobo.world/api-keys](https://enterprise.jobo.world/api-keys)
- **GitHub** — [github.com/Prakkie91/jobo-dotnet](https://github.com/Prakkie91/jobo-dotnet)
- **NuGet** — [nuget.org/packages/Jobo.Enterprise.Client](https://www.nuget.org/packages/Jobo.Enterprise.Client)

## License

MIT — see [LICENSE](LICENSE).
