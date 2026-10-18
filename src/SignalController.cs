using System.Net;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.LiveTv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SignalMonitor;

[ApiController]
[Route("SignalMonitor/Signal")]
[Authorize(Policy = "RequiresElevation")]
public sealed class SignalController(IConfigurationManager configuration) : ControllerBase
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(3), MaxResponseContentBufferSize = 128 * 1024 };

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> Get(CancellationToken cancellationToken)
    {
        var options = configuration.GetConfiguration<LiveTvOptions>("livetv");
        var addresses = options.TunerHosts.Where(t => string.Equals(t.Type, "hdhomerun", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Url).Concat((Plugin.Instance?.Configuration.TunerUrls ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var readings = await Task.WhenAll(addresses.Select(a => ReadTunerAsync(a, Client, cancellationToken)));
        return Ok(new { UpdatedAt = DateTimeOffset.UtcNow, Devices = readings });
    }

    public static async Task<DeviceReading> ReadTunerAsync(string address, HttpClient client, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(address.Contains("://", StringComparison.Ordinal) ? address : "http://" + address, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return new("Invalid tuner address", [], "Use an HTTP or HTTPS tuner address without a path or credentials.");
        var label = uri.Authority;
        try
        {
            using var response = await client.GetAsync(new Uri(uri, "status.json"), cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException();
            var tuners = document.RootElement.EnumerateArray().Select(t => t.ValueKind != JsonValueKind.Object ? throw new JsonException() : new TunerReading(
                Text(t, "Resource") ?? "Tuner", Text(t, "VctNumber"), Text(t, "VctName"),
                Number(t, "SignalStrengthPercent"), Number(t, "SignalQualityPercent"), Number(t, "SymbolQualityPercent"), Number(t, "NetworkRate"))).ToArray();
            return new(label, tuners, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(label, [], "Tuner did not respond within three seconds."); }
        catch (HttpRequestException) { return new(label, [], "Cannot reach tuner. Check its address, power, and network connection."); }
        catch (JsonException) { return new(label, [], "Tuner returned an unreadable status response."); }
    }

    private static string? Text(JsonElement item, string key) => item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static long? Number(JsonElement item, string key) => item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : null;
}
public sealed record DeviceReading(string Address, TunerReading[] Tuners, string? Error);
public sealed record TunerReading(string Resource, string? Channel, string? Name, long? Strength, long? Quality, long? Symbol, long? NetworkRate);

