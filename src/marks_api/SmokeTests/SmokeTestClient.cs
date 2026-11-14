using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace marks_api.SmokeTests;

public sealed class SmokeTestClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public SmokeTestClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<SmokeTestResponse> SendAsync(
        HttpMethod method,
        string path,
        string? token = null,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await _http.SendAsync(
            request,
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(
            cancellationToken);

        return new SmokeTestResponse(
            (int)response.StatusCode,
            content);
    }

    public void Dispose() => _http.Dispose();
}

public sealed record SmokeTestResponse(int StatusCode, string Body)
{
    public JsonElement? Json
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Body))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(Body);

                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    public string? GetString(string propertyName)
    {
        var json = Json;

        return json is JsonElement element &&
               element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    public int? GetInt(string propertyName)
    {
        var json = Json;

        return json is JsonElement element &&
               element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out var value)
            ? value
            : null;
    }

    public bool HasProperty(string propertyName)
    {
        var json = Json;

        return json is JsonElement element &&
               element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out _);
    }

    public int? ArrayLength()
    {
        var json = Json;

        return json is JsonElement element &&
               element.ValueKind == JsonValueKind.Array
            ? element.GetArrayLength()
            : null;
    }
}
