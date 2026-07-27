namespace Jobo.Enterprise.Client.Exceptions;

/// <summary>
/// Base exception for all Jobo client errors.
/// </summary>
public class JoboException : Exception
{
    public int? StatusCode { get; }
    public string? Detail { get; }
    public string? ResponseBody { get; }

    /// <summary>
    /// Stable machine-readable problem code, when the API supplies one
    /// (for example <c>feed_cursor_restart_required</c>).
    /// </summary>
    public string? Code { get; }

    public string? ApiVersion { get; }

    public JoboException(
        string message,
        int? statusCode = null,
        string? detail = null,
        string? responseBody = null,
        string? code = null,
        string? apiVersion = null)
        : base(message)
    {
        StatusCode = statusCode;
        Detail = detail;
        ResponseBody = responseBody;
        Code = code;
        ApiVersion = apiVersion;
    }
}

/// <summary>
/// Raised when the API key is missing or invalid (401).
/// </summary>
public class JoboAuthenticationException : JoboException
{
    public JoboAuthenticationException(
        string message,
        int? statusCode = null,
        string? detail = null,
        string? responseBody = null,
        string? code = null,
        string? apiVersion = null)
        : base(message, statusCode, detail, responseBody, code, apiVersion) { }
}

/// <summary>
/// Raised when the key is valid but not entitled to the resource (403).
/// <para>
/// The managed feed throws this for sandbox and marketplace keys, which carry
/// no customer account and therefore no managed job sources.
/// </para>
/// </summary>
public class JoboPermissionException : JoboException
{
    public JoboPermissionException(
        string message,
        int? statusCode = null,
        string? detail = null,
        string? responseBody = null,
        string? code = null,
        string? apiVersion = null)
        : base(message, statusCode, detail, responseBody, code, apiVersion) { }
}

/// <summary>
/// Raised when the requested resource does not exist (404).
/// </summary>
public class JoboNotFoundException : JoboException
{
    public JoboNotFoundException(
        string message,
        int? statusCode = null,
        string? detail = null,
        string? responseBody = null,
        string? code = null,
        string? apiVersion = null)
        : base(message, statusCode, detail, responseBody, code, apiVersion) { }
}

/// <summary>
/// Raised when the rate limit is exceeded (429) and retries are exhausted.
/// </summary>
public class JoboRateLimitException : JoboException
{
    public int? RetryAfterSeconds { get; }

    public JoboRateLimitException(
        string message,
        int? retryAfterSeconds = null,
        int? statusCode = null,
        string? detail = null,
        string? responseBody = null,
        string? code = null,
        string? apiVersion = null)
        : base(message, statusCode, detail, responseBody, code, apiVersion)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}

/// <summary>
/// Raised when the request is invalid (400).
/// </summary>
public class JoboValidationException : JoboException
{
    public JoboValidationException(
        string message,
        int? statusCode = null,
        string? detail = null,
        string? responseBody = null,
        string? code = null,
        string? apiVersion = null)
        : base(message, statusCode, detail, responseBody, code, apiVersion) { }
}

/// <summary>
/// Raised when a feed cursor can no longer be continued (409).
/// <para>
/// A legacy non-stable scan reached the deep-pagination boundary. The cursor
/// cannot be retried — discard it and start a new scan, leaving
/// <c>StableScan</c> at its default.
/// </para>
/// </summary>
public class JoboCursorRestartRequiredException : JoboException
{
    public JoboCursorRestartRequiredException(
        string message,
        int? statusCode = null,
        string? detail = null,
        string? responseBody = null,
        string? code = null,
        string? apiVersion = null)
        : base(message, statusCode, detail, responseBody, code, apiVersion) { }
}

/// <summary>
/// Raised when the server returns a 5xx error.
/// </summary>
public class JoboServerException : JoboException
{
    public JoboServerException(
        string message,
        int? statusCode = null,
        string? detail = null,
        string? responseBody = null,
        string? code = null,
        string? apiVersion = null)
        : base(message, statusCode, detail, responseBody, code, apiVersion) { }
}
