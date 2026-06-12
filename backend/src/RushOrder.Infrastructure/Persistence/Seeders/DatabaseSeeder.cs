using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Infrastructure.Persistence.Seeders;

public sealed class DatabaseSeeder
{
    // Stable plan ID (no Plan entity yet — just a foreign key)
    private static readonly Guid DemoPlanId = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    // Category IDs — stable Guids used as foreign keys in Product
    private static readonly Guid StartersCatId  = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid MainsCatId     = Guid.Parse("c1000000-0000-0000-0000-000000000002");
    private static readonly Guid DessertsCatId  = Guid.Parse("c1000000-0000-0000-0000-000000000003");
    private static readonly Guid DrinksCatId    = Guid.Parse("c1000000-0000-0000-0000-000000000004");

    public async Task SeedDevelopmentDataAsync(AppDbContext ctx, CancellationToken ct = default)
    {
        // Idempotent guard — Tenant has no query filter (BaseEntity)
        if (await ctx.Tenants.AnyAsync(t => t.Slug == "rincon-chef", ct)) return;

        // ── 1. Tenant ────────────────────────────────────────────────────────
        var tenant = Tenant.Create(
            "El Rincón del Chef", "rincon-chef", DemoPlanId,
            trialEndsAt: DateTimeOffset.UtcNow.AddDays(30));
        await ctx.Tenants.AddAsync(tenant, ct);
        await ctx.SaveChangesAsync(ct);
        var tenantId = tenant.Id;

        // ── 2. Restaurant ────────────────────────────────────────────────────
        var restaurant = Restaurant.Create(
            tenantId,
            name:     "El Rincón del Chef",
            address:  "Calle Mayor 42, 28013 Madrid",
            phone:    "+34 91 555 0100",
            email:    "info@rinconchef.es",
            taxRate:  0.10m,
            timezone: "Europe/Madrid",
            currency: "EUR");
        restaurant.SetImages(null, null);
        await ctx.Restaurants.AddAsync(restaurant, ct);
        await ctx.SaveChangesAsync(ct);
        var restaurantId = restaurant.Id;

        // ── 3. Tables — 3 zones × 5 tables ──────────────────────────────────
        string[] zones    = ["Terraza", "Interior", "Privado"];
        int[]    capacities = [2, 2, 4, 4, 6];
        var tables = new List<Table>();

        for (var z = 0; z < 3; z++)
        {
            for (var t = 0; t < 5; t++)
            {
                var table = Table.Create(
                    tenantId, restaurantId,
                    name:     $"Mesa {z * 5 + t + 1}",
                    capacity: capacities[t],
                    zone:     zones[z]);
                table.SetPosition(t * 150d, z * 200d);
                tables.Add(table);
            }
        }

        await ctx.Tables.AddRangeAsync(tables, ct);
        await ctx.SaveChangesAsync(ct);

        // ── 4. Users ─────────────────────────────────────────────────────────
        var hasher = new PasswordHasher<object>();
        string Hash(string pwd) => hasher.HashPassword(null!, pwd);

        User[] users =
        [
            User.Create(tenantId, "owner@demo.com",   Hash("Demo1234!"), "Carlos",  "García",   UserRole.Owner),
            User.Create(tenantId, "manager@demo.com", Hash("Demo1234!"), "Sofía",   "Martínez", UserRole.Manager),
            User.Create(tenantId, "waiter@demo.com",  Hash("Demo1234!"), "Miguel",  "López",    UserRole.Waiter),
            User.Create(tenantId, "kitchen@demo.com", Hash("Demo1234!"), "Lucía",   "Sánchez",  UserRole.Kitchen),
        ];

        foreach (var u in users)
            u.AddRestaurant(restaurantId);

        await ctx.Users.AddRangeAsync(users, ct);
        await ctx.SaveChangesAsync(ct);

        // ── 5. Products — 20 items in 4 categories ───────────────────────────
        var products = CreateProducts(tenantId, restaurantId);
        await ctx.Products.AddRangeAsync(products, ct);
        await ctx.SaveChangesAsync(ct);

        // ── 6. Sample orders in 5 different states ───────────────────────────
        var sampleOrders = CreateSampleOrders(tenantId, restaurantId, tables, products, users[2].Id);
        await ctx.Orders.AddRangeAsync(sampleOrders, ct);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task SeedProductionDataAsync(AppDbContext ctx, CancellationToken ct = default)
    {
        if (await ctx.Tenants.AnyAsync(t => t.Slug == "rushorder-system", ct)) return;

        var systemTenant = Tenant.Create("Rush Order System", "rushorder-system", DemoPlanId);
        await ctx.Tenants.AddAsync(systemTenant, ct);
        await ctx.SaveChangesAsync(ct);

        var hasher = new PasswordHasher<object>();
        // Random password on first boot — must be changed via password-reset flow
        var adminUser = User.Create(
            systemTenant.Id,
            "admin@rushorder.app",
            hasher.HashPassword(null!, Guid.NewGuid().ToString("N")),
            "System", "Admin",
            UserRole.Admin);

        await ctx.Users.AddAsync(adminUser, ct);
        await ctx.SaveChangesAsync(ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<Product> CreateProducts(Guid tenantId, Guid restaurantId)
    {
        Money EUR(decimal amount) => new(amount, "EUR");

        var items = new List<(Guid cat, string name, string desc, decimal price, int prep, string[] allergens, ProductTag[] tags)>
        {
            // Entrantes (4)
            (StartersCatId,  "Pan con tomate",              "Pan de cristal con tomate rallado, aceite de oliva virgen extra y sal marina",  3.50m, 5,  [],                                   [ProductTag.Vegetarian, ProductTag.Vegan]),
            (StartersCatId,  "Croquetas de jamón ibérico",  "Croquetas caseras de jamón ibérico de bellota con bechamel cremosa (6 uds)",     8.50m, 12, ["gluten","leche","huevo"],           [ProductTag.Popular]),
            (StartersCatId,  "Gazpacho andaluz",             "Gazpacho tradicional con verduras de temporada, servido frío",                  5.50m, 5,  [],                                   [ProductTag.Vegetarian, ProductTag.Vegan, ProductTag.GlutenFree]),
            (StartersCatId,  "Tabla de ibéricos",            "Selección de jamón ibérico, lomo, chorizo y salchichón con pan de hogaza",      18.00m, 10, [],                                  [ProductTag.GlutenFree, ProductTag.Popular]),

            // Principales (8)
            (MainsCatId,     "Paella valenciana",            "Paella tradicional con pollo, conejo y verduras frescas (mín. 2 personas)",     14.50m, 35, ["mariscos","gluten"],                [ProductTag.Popular]),
            (MainsCatId,     "Cocido madrileño",             "Cocido completo con garbanzos, verduras, carnes y embutidos artesanos",         16.00m, 60, ["gluten","apio"],                    []),
            (MainsCatId,     "Pulpo a la gallega",           "Pulpo cocido a la perfección sobre cama de patata y pimentón de la Vera",       19.50m, 15, ["mariscos"],                         [ProductTag.GlutenFree, ProductTag.Popular]),
            (MainsCatId,     "Cordero al horno",             "Paletilla de cordero lechal asada lentamente con romero y vino blanco",         22.00m, 20, [],                                   [ProductTag.GlutenFree]),
            (MainsCatId,     "Merluza en salsa verde",       "Merluza fresca del Cantábrico con almejas en salsa verde con perejil",          17.50m, 20, ["pescado","mariscos"],               [ProductTag.GlutenFree]),
            (MainsCatId,     "Entrecot de ternera (250g)",   "Entrecot de ternera gallega a la parrilla con guarnición y salsa a elegir",     24.00m, 15, [],                                   [ProductTag.GlutenFree]),
            (MainsCatId,     "Pisto manchego",               "Pisto de temporada con tomate, calabacín, pimiento y huevo escalfado",          9.50m,  15, ["huevo"],                            [ProductTag.Vegetarian, ProductTag.GlutenFree]),
            (MainsCatId,     "Risotto de setas silvestres",  "Risotto cremoso con setas del bosque, parmesano y trufa negra",                 13.50m, 20, ["leche","gluten"],                   [ProductTag.Vegetarian, ProductTag.New]),

            // Postres (4)
            (DessertsCatId,  "Crema catalana",               "Clásica crema catalana con azúcar caramelizado y canela",                       6.50m,  5,  ["leche","huevo","gluten"],           [ProductTag.Popular]),
            (DessertsCatId,  "Tarta de queso casera",        "Tarta de queso al horno estilo La Viña, con mermelada de frutos rojos",         7.00m,  5,  ["leche","huevo","gluten"],           [ProductTag.Popular]),
            (DessertsCatId,  "Coulant de chocolate",         "Bizcocho tibio de chocolate negro con corazón fundente y helado de vainilla",    7.50m,  12, ["leche","huevo","gluten"],           [ProductTag.New]),
            (DessertsCatId,  "Helado artesano (3 bolas)",    "Selección de helados artesanos del día con barquillos",                         5.50m,  5,  ["leche"],                            [ProductTag.Vegetarian]),

            // Bebidas (4)
            (DrinksCatId,    "Agua mineral (50cl)",          "Agua mineral natural o con gas",                                                1.50m,  1,  [],                                   [ProductTag.Vegan, ProductTag.GlutenFree]),
            (DrinksCatId,    "Vino tinto de la casa (copa)", "Vino tinto de la Ribera del Duero, copa de 150ml",                             3.50m,  1,  ["sulfitos"],                         [ProductTag.Vegan]),
            (DrinksCatId,    "Cerveza artesana (33cl)",      "Cerveza artesana local, elaborada con ingredientes ecológicos",                  3.00m,  1,  ["gluten","cebada"],                  [ProductTag.Vegan]),
            (DrinksCatId,    "Zumo natural del día",         "Zumo recién exprimido de naranja, pomelo o zanahoria",                          4.00m,  5,  [],                                   [ProductTag.Vegetarian, ProductTag.Vegan, ProductTag.GlutenFree]),
        };

        var products = new List<Product>();
        var sort = 0;

        foreach (var (cat, name, desc, price, prep, allergens, tags) in items)
        {
            var p = Product.Create(tenantId, restaurantId, cat, name, EUR(price), prep);
            // Description, allergens, tags set via reflection-safe domain methods
            p.UpdateDetails(name, desc, EUR(price), prep);
            foreach (var a in allergens) p.AddAllergen(a);
            foreach (var t in tags)      p.AddTag(t);
            p.SetSortOrder(sort++);
            products.Add(p);
        }

        return products;
    }

    private static List<Order> CreateSampleOrders(
        Guid tenantId, Guid restaurantId,
        List<Table> tables, List<Product> products,
        Guid waiterId)
    {
        var orders = new List<Order>();

        // 1. PENDING — Mesa 1, 2 items
        var o1 = Order.Create(tenantId, restaurantId, tables[0].Id, 1,
            OrderSource.QR, 0.10m, "EUR", waiterId: waiterId,
            notes: "Sin gluten en todo");
        o1.AddItem(products[2].Id, products[2].Name, products[2].Price, 1);  // Gazpacho
        o1.AddItem(products[3].Id, products[3].Name, products[3].Price, 2);  // Tabla ibéricos x2
        orders.Add(o1);

        // 2. CONFIRMED — Mesa 2, 3 items
        var o2 = Order.Create(tenantId, restaurantId, tables[1].Id, 2, OrderSource.Manual, 0.10m, "EUR");
        o2.AddItem(products[1].Id, products[1].Name, products[1].Price, 1);  // Croquetas
        o2.AddItem(products[4].Id, products[4].Name, products[4].Price, 2);  // Paella x2 pers
        o2.AddItem(products[16].Id, products[16].Name, products[16].Price, 2); // Agua x2
        o2.Confirm();
        orders.Add(o2);

        // 3. PREPARING — Mesa 3, 2 items
        var o3 = Order.Create(tenantId, restaurantId, tables[2].Id, 3, OrderSource.QR, 0.10m, "EUR");
        o3.AddItem(products[6].Id, products[6].Name, products[6].Price, 1);  // Pulpo
        o3.AddItem(products[9].Id, products[9].Name, products[9].Price, 1);  // Entrecot
        o3.Confirm();
        o3.StartPreparation();
        orders.Add(o3);

        // 4. READY — Mesa 4, 2 items
        var o4 = Order.Create(tenantId, restaurantId, tables[3].Id, 4, OrderSource.QR, 0.10m, "EUR");
        o4.AddItem(products[12].Id, products[12].Name, products[12].Price, 2);  // Crema catalana x2
        o4.AddItem(products[19].Id, products[19].Name, products[19].Price, 2);  // Zumo x2
        o4.Confirm();
        o4.StartPreparation();
        o4.MarkReady();
        orders.Add(o4);

        // 5. PAID — Mesa 5, 3 items (full lifecycle)
        var o5 = Order.Create(tenantId, restaurantId, tables[4].Id, 5, OrderSource.QR, 0.10m, "EUR");
        o5.AddItem(products[0].Id,  products[0].Name,  products[0].Price,  2);  // Pan con tomate x2
        o5.AddItem(products[7].Id,  products[7].Name,  products[7].Price,  1);  // Cordero
        o5.AddItem(products[13].Id, products[13].Name, products[13].Price, 1);  // Tarta de queso
        o5.Confirm();
        o5.StartPreparation();
        o5.MarkReady();
        o5.MarkServed();
        o5.Pay();
        orders.Add(o5);

        return orders;
    }
}
