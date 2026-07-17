using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Services;

// Free, keyless public holiday API (https://date.nager.at) — covers Spain's
// national holidays. It does NOT provide autonomous-community holidays
// (Catalonia's local ones like Sant Esteve/Sant Joan/La Diada), since those
// vary per region and aren't part of this API's data set; the demand
// forecast's holiday multiplier only accounts for national holidays as a
// result. A per-restaurant manual holiday list would be the way to close
// that gap — not implemented here.
public sealed class NagerDateHolidayProvider : IHolidayProvider
{
    private const string CountryCode = "ES";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);

    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly ILogger<NagerDateHolidayProvider> _logger;

    public NagerDateHolidayProvider(HttpClient httpClient, IDistributedCache cache, ILogger<NagerDateHolidayProvider> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlySet<DateOnly>> GetHolidaysAsync(int year, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"holidays:{CountryCode}:{year}";

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
            return JsonSerializer.Deserialize<HashSet<DateOnly>>(cached)!;

        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<NagerHoliday>>(
                $"api/v3/PublicHolidays/{year}/{CountryCode}", cancellationToken);

            var holidays = (response ?? [])
                .Select(h => DateOnly.Parse(h.Date))
                .ToHashSet();

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(holidays),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
                cancellationToken);

            return holidays;
        }
        catch (Exception ex)
        {
            // Holiday data is an enhancement to the forecast, not a
            // requirement — degrade to "no known holidays" rather than
            // failing the whole nightly forecast run.
            _logger.LogWarning(ex, "Could not fetch public holidays for {Year} from date.nager.at", year);
            return new HashSet<DateOnly>();
        }
    }

    private sealed class NagerHoliday
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;
    }
}
