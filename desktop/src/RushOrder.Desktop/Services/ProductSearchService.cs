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

    public ProductSearchService(AppState state, ILogger<ProductSearchService> logger)
    {
        _state  = state;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return _mockCatalog.Take(10).ToList();
        query = query.ToLowerInvariant();

        try
        {
            SetAuth();
            var id  = _state.CurrentRestaurant?.Id;
            var res = await _http.GetAsync(
                $"http://localhost:5000/api/products/search?restaurantId={id}&q={Uri.EscapeDataString(query)}&pageSize=20", ct);
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync(ct);
                return JsonConvert.DeserializeObject<List<ProductDto>>(json)!;
            }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Product search offline; using local catalog"); }

        return _mockCatalog
            .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || p.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(15)
            .ToList();
    }

    public async Task<IReadOnlyList<ProductDto>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return _mockCatalog
            .Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase) && p.IsAvailable)
            .ToList();
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
