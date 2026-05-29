// HostHealthProbe.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli;

/// <summary>
/// Probes the host's <c>/health</c> endpoint over HTTP. A 2xx response means the host is up; a
/// connection failure or timeout means it is not (reported as unhealthy, never thrown).
/// </summary>
public static class HostHealthProbe
{
    /// <summary>The host's health URL on localhost.</summary>
    public const string HealthUrl = "http://127.0.0.1:6300/health";

    /// <summary>Returns true when the host answers <paramref name="healthUrl"/> with a success status.</summary>
    public static async Task<bool> IsHealthyAsync(HttpClient client, string healthUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrEmpty(healthUrl);
        bool healthy = false;
        try
        {
            HttpResponseMessage response = await client.GetAsync(healthUrl, ct).ConfigureAwait(false);
            healthy = response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            // Host not listening → unhealthy, not an error.
        }
        catch (TaskCanceledException)
        {
            // Probe timed out → unhealthy, not an error.
        }
        return healthy;
    }
}
