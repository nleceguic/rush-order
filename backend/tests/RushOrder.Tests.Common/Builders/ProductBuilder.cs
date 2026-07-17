using RushOrder.Domain.Entities;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Tests.Common.Builders;

public sealed class ProductBuilder
{
    private Guid   _tenantId     = TenantDefaults.TenantId;
    private Guid   _restaurantId = TenantDefaults.RestaurantId;
    private Guid   _categoryId   = TenantDefaults.CategoryId;
    private string _name         = "Test Product";
    private Money  _price        = new(9.99m, "EUR");
    private bool   _unavailable  = false;

    public ProductBuilder WithName(string name)           { _name = name; return this; }
    public ProductBuilder WithPrice(Money price)          { _price = price; return this; }
    public ProductBuilder WithRestaurant(Guid id)         { _restaurantId = id; return this; }
    public ProductBuilder WithCategory(Guid id)           { _categoryId = id; return this; }
    public ProductBuilder Unavailable()                   { _unavailable = true; return this; }

    public Product Build()
    {
        var product = Product.Create(_tenantId, _restaurantId, _categoryId, _name, _price);
        if (_unavailable) product.SetAvailability(false);
        return product;
    }
}
