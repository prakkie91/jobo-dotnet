using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobo.Enterprise.Client.Exceptions;

namespace Jobo.Enterprise.Client;

/// <summary>
/// Shared HTTP infrastructure for all Jobo sub-clients.
/// <para>
/// Transient statuses (429, 503) are retried with bounded backoff honouring
/// <c>Retry-After</c>; everything else throws a typed
/// <see cref="JoboException"/> straight away.
/// </para>
/// </summary>
public abstract class JoboClientBase
{
    internal readonly HttpClient HttpClient;

    /// <summary>
    /// Per-request timeout, applied through a linked cancellation token. Null
    /// leaves timing out to the caller's own <see cref="HttpClient"/>.
    /// </summary>
    internal readonly TimeSpan? RequestTimeout;

    /// <summary>Statuses the API documents as transient.</summary>
    private static readonly HashSet<int> RetryStatuses = new() { 429, 503 };

    /// <summary>Retries *after* the initial attempt, so 3 means at most 4 requests.</summary>
    private const int MaxRetries = 3;

    /// <summary>Upper bound on a single backoff delay, including a server <c>Retry-After</c>.</summary>
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    internal JoboClientBase(HttpClient httpClient, TimeSpan? requestTimeout = null)
    {
        HttpClient = httpClient;
        RequestTimeout = requestTimeout;
    }

    internal Task<T> PostAsync<T>(string path, object body, CancellationToken ct) where T : new() =>
        PostAsync<T>(path, body, null, ct);

    internal async Task<T> PostAsync<T>(string path, object body, TimeSpan? timeout, CancellationToken ct)
        where T : new()
    {
        var json = JsonSerializer.Serialize(body, body.GetType(), JsonOptions);
        var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            },
            timeout,
            ct);
        return await ReadAsync<T>(response, ct);
    }

    internal Task<T> GetAsync<T>(string path, CancellationToken ct) where T : new() =>
        GetAsync<T>(path, null, ct);

    internal async Task<T> GetAsync<T>(string path, TimeSpan? timeout, CancellationToken ct) where T : new()
    {
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, path), timeout, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct) where T : new()
    {
        using (response)
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct) ?? new T();
        }
    }

    /// <summary>
    /// Sends the request, retrying transient statuses. The factory is called
    /// per attempt because an <see cref="HttpRequestMessage"/> cannot be reused.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        TimeSpan? timeout,
        CancellationToken ct)
    {
        var effectiveTimeout = timeout ?? RequestTimeout;

        for (var attempt = 0; ; attempt++)
        {
            using var timeoutSource = effectiveTimeout is not null
                ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                : null;
            timeoutSource?.CancelAfter(effectiveTimeout!.Value);
            var token = timeoutSource?.Token ?? ct;

            var response = await HttpClient.SendAsync(requestFactory(), token);
            if (response.IsSuccessStatusCode) return response;

            if (RetryStatuses.Contains((int)response.StatusCode) && attempt < MaxRetries)
            {
                var delay = BackoffFor(response, attempt);
                response.Dispose();
                await Task.Delay(delay, ct);
                continue;
            }

            using (response)
            {
                await ThrowForStatusAsync(response, ct);
            }
        }
    }

    private static TimeSpan BackoffFor(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.TryGetValues("Retry-After", out var values)
            && double.TryParse(values.FirstOrDefault(), out var seconds))
        {
            var advised = TimeSpan.FromSeconds(seconds);
            return advised < MaxBackoff ? advised : MaxBackoff;
        }

        var backoff = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt));
        return backoff < MaxBackoff ? backoff : MaxBackoff;
    }

    internal static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        await ThrowForStatusAsync(response, ct);
    }

    private static async Task ThrowForStatusAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        var status = (int)response.StatusCode;
        var (detail, code, apiVersion) = ReadProblem(body);
        var message = !string.IsNullOrEmpty(detail) ? $"HTTP {status}: {detail}" : $"HTTP {status}";

        throw status switch
        {
            401 => new JoboAuthenticationException(message, status, detail, body, code, apiVersion),
            403 => new JoboPermissionException(message, status, detail, body, code, apiVersion),
            404 => new JoboNotFoundException(message, status, detail, body, code, apiVersion),
            409 => new JoboCursorRestartRequiredException(message, status, detail, body, code, apiVersion),
            429 => new JoboRateLimitException(
                message,
                response.Headers.TryGetValues("Retry-After", out var values)
                    ? int.TryParse(values.FirstOrDefault(), out var ra) ? ra : null
                    : null,
                status, detail, body, code, apiVersion),
            400 => new JoboValidationException(message, status, detail, body, code, apiVersion),
            >= 500 => new JoboServerException(message, status, detail, body, code, apiVersion),
            _ => new JoboException(message, status, detail, body, code, apiVersion)
        };
    }

    private static (string? Detail, string? Code, string? ApiVersion) ReadProblem(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var detail = root.TryGetProperty("detail", out var d) ? d.GetString()
                : root.TryGetProperty("error", out var e) ? e.GetString()
                : null;
            var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            var apiVersion = root.TryGetProperty("api_version", out var v) ? v.GetString() : null;
            return (detail, code, apiVersion);
        }
        catch
        {
            return (body, null, null);
        }
    }
}
