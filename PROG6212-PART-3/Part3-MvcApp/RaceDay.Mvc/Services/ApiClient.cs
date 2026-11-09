using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RaceDay.Mvc.Services;

public class ApiResponse
{
    public bool Ok { get; init; }
    public int StatusCode { get; init; }

    /// <summary>A friendly, user-safe message (never a raw server error).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The first validation message from the API, when it sent one (used for image upload errors).</summary>
    public string? Detail { get; init; }
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }
}

/// <summary>Thrown when the API session is no longer valid; a global filter sends the user back to the login page.</summary>
public class ApiSessionExpiredException : Exception
{
    public ApiSessionExpiredException() : base("The API session has expired.") { }
}

/// <summary>
/// The only way the MVC app gets or changes data. Forwards the logged-in user's API session cookie
/// (kept in the MVC server-side session) on every request.
/// </summary>
public class ApiClient
{
    public const string SessionKey = "ApiSessionCookie";
    private const string ApiCookieName = "RaceDay.Session";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, IHttpContextAccessor accessor, ILogger<ApiClient> logger)
    {
        _http = http;
        _accessor = accessor;
        _logger = logger;
    }

    private ISession? Session => _accessor.HttpContext?.Session;

    public Task<ApiResponse<T>> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url);

    public async Task<ApiResponse<T>> SendAsync<T>(HttpMethod method, string url, object? body = null, bool requireSession = true)
    {
        using var request = BuildRequest(method, url, body);
        var response = await SendRawAsync(request, requireSession);
        return await ToResponseAsync<T>(response);
    }

    /// <summary>For calls that return no content (DELETE, logout).</summary>
    public async Task<ApiResponse> SendAsync(HttpMethod method, string url, object? body = null, bool requireSession = true)
    {
        using var request = BuildRequest(method, url, body);
        var response = await SendRawAsync(request, requireSession);
        return await ToResponseAsync<object>(response);
    }

    public async Task<ApiResponse<T>> UploadAsync<T>(string url, IFormFile file)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(fileContent, "file", file.FileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        var response = await SendRawAsync(request, requireSession: true);
        return await ToResponseAsync<T>(response);
    }

    /// <summary>Fetches an image through the API. Returns null when there is no image or the API is unreachable.</summary>
    public async Task<(byte[] Bytes, string ContentType)?> GetImageAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await SendRawAsync(request, requireSession: false);
        if (response == null) return null;

        using (response)
        {
            if (!response.IsSuccessStatusCode) return null;
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return (bytes, response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
        }
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        return request;
    }

    private async Task<HttpResponseMessage?> SendRawAsync(HttpRequestMessage request, bool requireSession)
    {
        var cookie = Session?.GetString(SessionKey);

        // Logged in here, but the stored API session is gone (e.g. the MVC server restarted) -> log in again.
        if (requireSession && string.IsNullOrEmpty(cookie) && _accessor.HttpContext?.User.Identity?.IsAuthenticated == true)
            throw new ApiSessionExpiredException();

        if (!string.IsNullOrEmpty(cookie))
            request.Headers.TryAddWithoutValidation("Cookie", $"{ApiCookieName}={cookie}");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogError(ex, "Could not reach the RaceDay API for {Method} {Url}", request.Method, request.RequestUri);
            return null;
        }

        CaptureCookie(response);

        if (response.StatusCode == HttpStatusCode.Unauthorized && requireSession)
        {
            response.Dispose();
            throw new ApiSessionExpiredException();
        }

        return response;
    }

    /// <summary>Stores (or clears) the API session cookie from the response so later requests stay logged in.</summary>
    private void CaptureCookie(HttpResponseMessage response)
    {
        var session = Session;
        if (session == null || !response.Headers.TryGetValues("Set-Cookie", out var headers)) return;

        foreach (var header in headers)
        {
            if (!header.StartsWith(ApiCookieName + "=", StringComparison.Ordinal)) continue;

            var value = header.Substring(ApiCookieName.Length + 1).Split(';')[0];
            if (string.IsNullOrEmpty(value)) session.Remove(SessionKey);
            else session.SetString(SessionKey, value);
        }
    }

    private async Task<ApiResponse<T>> ToResponseAsync<T>(HttpResponseMessage? response)
    {
        if (response == null)
        {
            return new ApiResponse<T>
            {
                Ok = false,
                StatusCode = 0,
                Message = "We couldn't reach the RaceDay service. Please try again in a moment."
            };
        }

        using (response)
        {
            var status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                T? data = default;
                try
                {
                    var text = response.StatusCode == HttpStatusCode.NoContent
                        ? string.Empty
                        : await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                        data = JsonSerializer.Deserialize<T>(text, Json);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Unexpected response format from the API");
                    return new ApiResponse<T> { Ok = false, StatusCode = 500, Message = "We received an unexpected response. Please try again." };
                }

                return new ApiResponse<T> { Ok = true, StatusCode = status, Data = data };
            }

            var (title, detail, apiMessage) = await ReadProblemAsync(response);
            var message = status switch
            {
                400 => apiMessage ?? "Some of the details you entered are not valid. Please check them and try again.",
                401 => "You need to log in to do that.",
                403 => "You don't have permission to do that.",
                404 => "We couldn't find what you were looking for.",
                409 => apiMessage ?? "That conflicts with existing information.",
                503 => title ?? "A required service is temporarily unavailable. Please try again later.",
                _ => "Something went wrong on our side. Please try again."
            };

            return new ApiResponse<T> { Ok = false, StatusCode = status, Message = message, Detail = detail };
        }
    }

    /// <summary>Reads the API's error body: a ProblemDetails title, the first validation error, or a simple { "message": ... }.</summary>
    private static async Task<(string? Title, string? Detail, string? ApiMessage)> ReadProblemAsync(HttpResponseMessage response)
    {
        string? title = null;
        string? detail = null;
        string? apiMessage = null;
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(text)) return (null, null, null);

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (null, null, null);

            if (root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                title = t.GetString();

            if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                apiMessage = m.GetString();

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in errors.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Array) continue;
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String) { detail = item.GetString(); break; }
                    }
                    if (detail != null) break;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON - fall back to the generic message.
        }

        return (title, detail, apiMessage);
    }
}
