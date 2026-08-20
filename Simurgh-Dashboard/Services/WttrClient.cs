using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;

namespace SimurghDashboard.Services;

/// <summary>
/// Production-grade Weather service utilizing injected HttpClient.
/// </summary>
public sealed class WttrClient : IWeatherService
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance. Inject a shared HttpClient here.
    /// </summary>
    public WttrClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<WttrResponse?> GetWeatherAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Weather URL cannot be empty.", nameof(url));

        try
        {
            // System.Net.Http.Json is highly optimized for memory and streams automatically
            return await _httpClient.GetFromJsonAsync<WttrResponse>(url, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw; // Caller handles cancellation
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[WttrClient] Failed to fetch weather data. Exception: {ex.Message}");
            throw new InvalidOperationException("Weather Engine Failure", ex);
        }
    }
}