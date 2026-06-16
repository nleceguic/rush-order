using System.Net.Http.Json;
using RushOrder.Desktop.Models;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class MenuService
{
    private readonly AppState   _state;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };

    private List<CategoryDto>    _catCache  = [];
    private List<MenuProductDto> _prodCache = [];

    public MenuService(AppState state) => _state = state;

    // ── Categories ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync()
    {
        try
        {
            Auth();
            var result = await _http.GetFromJsonAsync<List<CategoryDto>>($"{Base()}/api/v1/categories");
            if (result is null) throw new();
            _catCache = result;
            return _catCache;
        }
        catch
        {
            if (_catCache.Count > 0) return _catCache;
            _catCache = MockCategories();
            return _catCache;
        }
    }

    public async Task<CategoryDto> SaveCategoryAsync(SaveCategoryRequest req)
    {
        try
        {
            Auth();
            if (req.Id is null)
            {
                var resp = await _http.PostAsJsonAsync($"{Base()}/api/v1/categories", req);
                var created = await resp.Content.ReadFromJsonAsync<CategoryDto>();
                if (created is not null) { _catCache.Add(created); return created; }
            }
            else
            {
                var resp = await _http.PutAsJsonAsync($"{Base()}/api/v1/categories/{req.Id}", req);
                var updated = await resp.Content.ReadFromJsonAsync<CategoryDto>();
                if (updated is not null)
                {
                    var idx = _catCache.FindIndex(c => c.Id == req.Id);
                    if (idx >= 0) _catCache[idx] = updated;
                    return updated;
                }
            }
        }
        catch { }

        // Optimistic local
        var local = new CategoryDto(req.Id ?? Guid.NewGuid(), req.Name, req.SortOrder, req.IsActive, 0, 0);
        var li = _catCache.FindIndex(c => c.Id == req.Id);
        if (li >= 0) _catCache[li] = local; else _catCache.Add(local);
        return local;
    }

    public async Task DeleteCategoryAsync(Guid id)
    {
        try { Auth(); await _http.DeleteAsync($"{Base()}/api/v1/categories/{id}"); }
        catch { }
        _catCache.RemoveAll(c => c.Id == id);
        _prodCache.RemoveAll(p => p.CategoryId == id);
    }

    public async Task ReorderCategoriesAsync(IList<Guid> orderedIds)
    {
        try
        {
            Auth();
            await _http.PutAsJsonAsync($"{Base()}/api/v1/categories/reorder",
                new { OrderedIds = orderedIds });
        }
        catch { }

        for (int i = 0; i < orderedIds.Count; i++)
        {
            var idx = _catCache.FindIndex(c => c.Id == orderedIds[i]);
            if (idx >= 0) _catCache[idx] = _catCache[idx] with { SortOrder = i };
        }
        _catCache.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
    }

    // ── Products ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<MenuProductDto>> GetProductsAsync(
        Guid? categoryId = null, string? search = null,
        bool? onlyAvailable = null, bool? lowStock = null)
    {
        try
        {
            Auth();
            var qs   = BuildProductQs(categoryId, search, onlyAvailable, lowStock);
            var resp = await _http.GetFromJsonAsync<List<MenuProductDto>>($"{Base()}/api/v1/menu/products{qs}");
            if (resp is null) throw new();
            _prodCache = resp;
        }
        catch
        {
            if (_prodCache.Count == 0) _prodCache = MockProducts();
        }

        return Filter(_prodCache, categoryId, search, onlyAvailable, lowStock);
    }

    public async Task<MenuProductDto> SaveProductAsync(SaveProductRequest req)
    {
        try
        {
            Auth();
            HttpResponseMessage resp;
            if (req.Id is null)
                resp = await _http.PostAsJsonAsync($"{Base()}/api/v1/menu/products", req);
            else
                resp = await _http.PutAsJsonAsync($"{Base()}/api/v1/menu/products/{req.Id}", req);

            var saved = await resp.Content.ReadFromJsonAsync<MenuProductDto>();
            if (saved is not null)
            {
                var pi = _prodCache.FindIndex(p => p.Id == saved.Id);
                if (pi >= 0) _prodCache[pi] = saved; else _prodCache.Add(saved);
                return saved;
            }
        }
        catch { }

        var local = BuildLocalProduct(req);
        var li = _prodCache.FindIndex(p => p.Id == (req.Id ?? local.Id));
        if (li >= 0) _prodCache[li] = local; else _prodCache.Add(local);
        return local;
    }

    public async Task DeleteProductAsync(Guid id)
    {
        try { Auth(); await _http.DeleteAsync($"{Base()}/api/v1/menu/products/{id}"); }
        catch { }
        _prodCache.RemoveAll(p => p.Id == id);
    }

    public async Task<MenuProductDto> ToggleAvailabilityAsync(Guid id, bool available)
    {
        try
        {
            Auth();
            await _http.PatchAsync($"{Base()}/api/v1/menu/products/{id}/availability",
                JsonContent.Create(new { IsAvailable = available }));
        }
        catch { }

        var idx = _prodCache.FindIndex(p => p.Id == id);
        if (idx >= 0)
        {
            _prodCache[idx] = _prodCache[idx] with { IsAvailable = available };
            return _prodCache[idx];
        }
        throw new InvalidOperationException("Product not found");
    }

    public async Task<MenuProductDto> DuplicateProductAsync(Guid id)
    {
        var src = _prodCache.FirstOrDefault(p => p.Id == id)
                  ?? throw new InvalidOperationException("Product not found");
        var req = new SaveProductRequest(
            null, $"{src.Name} (copia)", src.CategoryId, src.Price, false,
            src.Description, src.PrepTimeMinutes, src.Allergens, src.Tags,
            false, null, null, src.Variants, src.ModifierGroups,
            _prodCache.Count(p => p.CategoryId == src.CategoryId), null, null);
        return await SaveProductAsync(req);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void Auth() =>
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _state.AccessToken);

    private string Base() => "http://localhost:5000";

    private static string BuildProductQs(Guid? cat, string? search, bool? avail, bool? low)
    {
        var parts = new List<string>();
        if (cat.HasValue)   parts.Add($"categoryId={cat}");
        if (!string.IsNullOrWhiteSpace(search)) parts.Add($"search={Uri.EscapeDataString(search)}");
        if (avail == true)  parts.Add("onlyAvailable=true");
        if (low == true)    parts.Add("lowStock=true");
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    private static IReadOnlyList<MenuProductDto> Filter(
        IEnumerable<MenuProductDto> src, Guid? cat, string? search, bool? avail, bool? low)
    {
        var q = src.AsEnumerable();
        if (cat.HasValue)    q = q.Where(p => p.CategoryId == cat.Value);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (avail == true)   q = q.Where(p => p.IsAvailable);
        if (low == true)     q = q.Where(p => p.IsLowStock);
        return q.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToList();
    }

    private static MenuProductDto BuildLocalProduct(SaveProductRequest r) => new(
        r.Id ?? Guid.NewGuid(), r.Name, r.CategoryId, "", r.Price, r.IsAvailable, null,
        r.Description, r.PrepTimeMinutes, r.Allergens, r.Tags, r.TrackStock,
        r.StockQuantity, r.StockAlertThreshold, r.Variants, r.ModifierGroups,
        r.SortOrder, r.InternalRef, r.InternalNotes);

    // ── Mock data ─────────────────────────────────────────────────────────

    private static List<CategoryDto> MockCategories() =>
    [
        new(Guid.Parse("10000000-0000-0000-0000-000000000001"), "Entrantes",   0, true, 4, 0),
        new(Guid.Parse("10000000-0000-0000-0000-000000000002"), "Principales", 1, true, 5, 1),
        new(Guid.Parse("10000000-0000-0000-0000-000000000003"), "Postres",     2, true, 3, 0),
        new(Guid.Parse("10000000-0000-0000-0000-000000000004"), "Bebidas",     3, true, 4, 0),
    ];

    private static List<MenuProductDto> MockProducts()
    {
        var cat1 = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var cat2 = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var cat3 = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var cat4 = Guid.Parse("10000000-0000-0000-0000-000000000004");

        ModifierGroup PuntoCoccion() => new(Guid.NewGuid(), "Punto de cocción", true, 1, 1,
        [
            new(Guid.NewGuid(), "Poco hecho",  0,   true),
            new(Guid.NewGuid(), "Al punto",    0,   true),
            new(Guid.NewGuid(), "Muy hecho",   0,   true),
        ]);

        return
        [
            new(Guid.NewGuid(), "Patatas bravas",     cat1, "Entrantes",   5.90m,  true,  null, "Patatas fritas con salsa brava casera", 8,  [],              ["Vegano","Popular"], false, null, null, [], [], 0, "ENT-001", null),
            new(Guid.NewGuid(), "Croquetas ibéricas", cat1, "Entrantes",   8.50m,  true,  null, "Croquetas de jamón ibérico",            15, ["Gluten","Lácteos","Huevos"], ["Popular"], false, null, null, [], [], 1, "ENT-002", null),
            new(Guid.NewGuid(), "Gazpacho andaluz",   cat1, "Entrantes",   4.50m,  true,  null, "Gazpacho tradicional con guarnición",   5,  [],              ["Vegetariano","Vegano","Sin Gluten"], false, null, null, [], [], 2, "ENT-003", null),
            new(Guid.NewGuid(), "Carpaccio de buey",  cat1, "Entrantes",  12.90m,  false, null, "Carpaccio de buey con parmesano",       10, ["Lácteos"],     ["Nuevo"], false, null, null, [], [], 3, "ENT-004", null),

            new(Guid.NewGuid(), "Chuletón 400g",      cat2, "Principales", 28.50m, true,  null, "Chuletón de buey a la brasa",          20, ["Gluten"],      [], false, null, null,
                [new(Guid.NewGuid(), "Guarnición patatas", 0, true), new(Guid.NewGuid(), "Guarnición ensalada", 0, true)],
                [PuntoCoccion()], 0, "PRI-001", null),
            new(Guid.NewGuid(), "Risotto de setas",   cat2, "Principales", 16.50m, true,  null, "Risotto cremoso de setas de temporada", 18, ["Gluten","Lácteos"], ["Vegetariano"], false, null, null, [], [], 1, "PRI-002", null),
            new(Guid.NewGuid(), "Paella valenciana",  cat2, "Principales", 18.00m, true,  null, "Paella para 2 personas (mín. 2 pax)",   35, ["Mariscos","Gluten"], ["Popular"], true, 8, 3, [], [], 2, "PRI-003", "Requiere 30 min de aviso previo"),
            new(Guid.NewGuid(), "Hamburguesa BBQ",    cat2, "Principales", 14.50m, true,  null, "Hamburguesa de ternera con bacon y queso", 12, ["Gluten","Lácteos"], [], false, null, null,
                [new(Guid.NewGuid(), "Sin queso", -1.00m, true), new(Guid.NewGuid(), "Extra bacon", 1.50m, true)],
                [PuntoCoccion()], 3, "PRI-004", null),
            new(Guid.NewGuid(), "Bacalao al pil-pil", cat2, "Principales", 19.90m, true,  null, "Bacalao confitado con salsa pil-pil",   22, ["Pescado"],     ["Sin Gluten"], false, null, null, [], [], 4, "PRI-005", null),

            new(Guid.NewGuid(), "Tarta de queso",     cat3, "Postres",     6.90m,  true,  null, "Tarta de queso al horno estilo La Viña", 0, ["Gluten","Lácteos","Huevos"], ["Popular"], false, null, null, [], [], 0, "POS-001", null),
            new(Guid.NewGuid(), "Tiramisú",           cat3, "Postres",     6.50m,  true,  null, "Tiramisú casero con bizcocho de soletilla", 0, ["Gluten","Lácteos","Huevos"], [], false, null, null, [], [], 1, "POS-002", null),
            new(Guid.NewGuid(), "Flan de huevo",      cat3, "Postres",     4.90m,  true,  null, "Flan casero con caramelo",               0, ["Lácteos","Huevos"], ["Sin Gluten"], false, null, null, [], [], 2, "POS-003", null),

            new(Guid.NewGuid(), "Agua mineral",       cat4, "Bebidas",     2.00m,  true,  null, "Botella 50cl", 0, [], ["Vegano","Sin Gluten"], true, 24, 6, [], [], 0, "BEB-001", null),
            new(Guid.NewGuid(), "Cerveza artesanal",  cat4, "Bebidas",     3.50m,  true,  null, "Cerveza local de temporada",   0, ["Gluten"], [], false, null, null, [], [], 1, "BEB-002", null),
            new(Guid.NewGuid(), "Vino de la casa",    cat4, "Bebidas",     4.50m,  true,  null, "Copa de vino tinto/blanco/rosado", 0, ["Sulfitos"], [], false, null, null,
                [new(Guid.NewGuid(), "Tinto",  0, true), new(Guid.NewGuid(), "Blanco", 0, true), new(Guid.NewGuid(), "Rosado", 0, true)],
                [], 2, "BEB-003", null),
            new(Guid.NewGuid(), "Café solo",          cat4, "Bebidas",     1.80m,  true,  null, null, 3, ["Lácteos"], [], false, null, null, [], [], 3, "BEB-004", null),
        ];
    }
}
