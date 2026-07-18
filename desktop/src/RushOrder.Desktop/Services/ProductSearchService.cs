using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class ProductSearchService
{
    private readonly AppState _state;
    private readonly ILogger<ProductSearchService> _logger;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(4) };

    private static readonly IReadOnlyList<ProductDto> _mockCatalog = BuildMockCatalog();

    // Populated lazily from the real backend and reused across searches — there's no
    // /search endpoint on the backend at all, so this filters client-side over the
    // full product list instead (see FetchLiveCatalogAsync).
    private List<ProductDto>? _liveCatalog;

    public ProductSearchService(AppState state, ILogger<ProductSearchService> logger)
    {
        _state  = state;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        var catalog = await GetCatalogAsync(ct);

        if (string.IsNullOrWhiteSpace(query)) return catalog.Take(10).ToList();
        query = query.ToLowerInvariant();

        return catalog
            .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || p.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(15)
            .ToList();
    }

    public async Task<IReadOnlyList<ProductDto>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        var catalog = await GetCatalogAsync(ct);
        return catalog
            .Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase) && p.IsAvailable)
            .ToList();
    }

    private async Task<IReadOnlyList<ProductDto>> GetCatalogAsync(CancellationToken ct)
    {
        if (_liveCatalog is not null) return _liveCatalog;

        try
        {
            var live = await FetchLiveCatalogAsync(ct);
            if (live.Count > 0) { _liveCatalog = live; return _liveCatalog; }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Product catalog offline; using local mock"); }

        return _mockCatalog;
    }

    private async Task<List<ProductDto>> FetchLiveCatalogAsync(CancellationToken ct)
    {
        SetAuth();
        var rid = _state.CurrentRestaurant?.Id;

        var catRes = await _http.GetAsync($"http://localhost:5143/api/v1/menu/categories?restaurantId={rid}", ct);
        var catNames = new Dictionary<Guid, string>();
        if (catRes.IsSuccessStatusCode)
        {
            var catJson = await catRes.Content.ReadAsStringAsync(ct);
            var cats = JsonConvert.DeserializeObject<ApiEnvelope<List<BackendCategoryDto>>>(catJson)?.Data ?? [];
            catNames = cats.ToDictionary(c => c.Id, c => c.Name);
        }

        var products = new List<ProductDto>();
        var page = 1;
        while (true)
        {
            var res = await _http.GetAsync(
                $"http://localhost:5143/api/v1/menu/products?restaurantId={rid}&page={page}&pageSize=100", ct);
            if (!res.IsSuccessStatusCode) break;

            var json = await res.Content.ReadAsStringAsync(ct);
            var batch = JsonConvert.DeserializeObject<ApiEnvelope<List<BackendProductSummaryDto>>>(json)?.Data ?? [];
            if (batch.Count == 0) break;

            products.AddRange(batch.Select(p => new ProductDto(
                p.Id, p.Name, catNames.GetValueOrDefault(p.CategoryId, ""), p.Price, p.IsAvailable, [])));

            if (batch.Count < 100) break;
            page++;
        }
        return products;
    }

    private void SetAuth() =>
        _http.DefaultRequestHeaders.Authorization = _state.AccessToken is { } t
            ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t)
            : null;

    private static IReadOnlyList<ProductDto> BuildMockCatalog() =>
    [
        // Entrantes
        new(Guid.NewGuid(), "Gazpacho andaluz",     "Entrantes",  5.00m, true,  []),
        new(Guid.NewGuid(), "Ensalada mixta",        "Entrantes",  7.50m, true,  []),
        new(Guid.NewGuid(), "Pulpo a la gallega",    "Entrantes", 18.00m, true,  ["Shellfish"]),
        new(Guid.NewGuid(), "Tortilla española",     "Entrantes",  9.50m, true,  ["Eggs", "Dairy"]),
        new(Guid.NewGuid(), "Croquetas caseras",     "Entrantes",  7.00m, true,  ["Gluten", "Dairy", "Eggs"]),
        new(Guid.NewGuid(), "Gambas al ajillo",      "Entrantes", 14.00m, true,  ["Shellfish", "Sulphites"]),

        // Principales
        new(Guid.NewGuid(), "Paella Valencia",       "Principales", 14.50m, true,  ["Shellfish", "Crustaceans"]),
        new(Guid.NewGuid(), "Chuletón 400g",         "Principales", 28.00m, true,  []),
        new(Guid.NewGuid(), "Merluza al horno",      "Principales", 21.00m, true,  ["Fish"]),
        new(Guid.NewGuid(), "Salmón a la plancha",   "Principales", 18.50m, true,  ["Fish"]),
        new(Guid.NewGuid(), "Cocido madrileño",      "Principales", 16.00m, true,  ["Gluten", "Nuts"]),
        new(Guid.NewGuid(), "Menú del día",          "Principales", 12.50m, true,  []),
        new(Guid.NewGuid(), "Cochinillo asado",      "Principales", 22.00m, true,  ["Sulphites"]),
        new(Guid.NewGuid(), "Patatas fritas",        "Guarniciones",  4.50m, true,  []),

        // Postres
        new(Guid.NewGuid(), "Postre variado",        "Postres",    4.00m, true,  ["Gluten", "Dairy", "Eggs"]),
        new(Guid.NewGuid(), "Tarta de queso",        "Postres",    5.50m, true,  ["Dairy", "Eggs", "Gluten"]),
        new(Guid.NewGuid(), "Crema catalana",        "Postres",    4.50m, true,  ["Dairy", "Eggs"]),
        new(Guid.NewGuid(), "Fruta de temporada",    "Postres",    3.50m, true,  []),

        // Bebidas
        new(Guid.NewGuid(), "Agua Mineral",          "Bebidas",    1.50m, true,  []),
        new(Guid.NewGuid(), "Agua con gas",          "Bebidas",    1.80m, true,  []),
        new(Guid.NewGuid(), "Cola Zero",             "Bebidas",    2.20m, true,  []),
        new(Guid.NewGuid(), "Zumo de naranja",       "Bebidas",    2.80m, true,  []),
        new(Guid.NewGuid(), "Caña",                  "Bebidas",    1.80m, true,  ["Gluten"]),
        new(Guid.NewGuid(), "Cerveza",               "Bebidas",    2.80m, true,  ["Gluten"]),
        new(Guid.NewGuid(), "Vino de la casa",       "Bebidas",    8.00m, true,  ["Sulphites"]),
        new(Guid.NewGuid(), "Sangría",               "Bebidas",   12.00m, true,  ["Sulphites"]),
        new(Guid.NewGuid(), "Café",                  "Bebidas",    1.50m, true,  []),
        new(Guid.NewGuid(), "Infusión",              "Bebidas",    1.80m, true,  []),
    ];
}
