terraform {
  required_providers {
    azurerm = { source = "hashicorp/azurerm"; version = "~> 3.110" }
  }
}

locals {
  prefix = "rush-order-${var.environment}"
}

resource "azurerm_redis_cache" "this" {
  name                = "${local.prefix}-redis"
  resource_group_name = var.resource_group_name
  location            = var.location
  capacity            = var.capacity
  family              = var.family
  sku_name            = var.sku_name
  enable_non_ssl_port = false
  minimum_tls_version = "1.2"

  redis_configuration {
    maxmemory_policy = "allkeys-lru"
  }

  tags = var.tags
}

# Private endpoint — Redis is not exposed to internet
resource "azurerm_private_endpoint" "redis" {
  name                = "${local.prefix}-redis-pe"
  resource_group_name = var.resource_group_name
  location            = var.location
  subnet_id           = var.private_endpoints_subnet_id

  private_service_connection {
    name                           = "${local.prefix}-redis-psc"
    private_connection_resource_id = azurerm_redis_cache.this.id
    is_manual_connection           = false
    subresource_names              = ["redisCache"]
  }

  private_dns_zone_group {
    name                 = "redis-dns-group"
    private_dns_zone_ids = [var.private_dns_zone_id]
  }

  tags = var.tags
}

# ── Geo-replication (Premium SKU required) ────────────────────────────────────
resource "azurerm_redis_cache" "secondary" {
  count               = var.enable_geo_replication ? 1 : 0
  name                = "${local.prefix}-redis-west"
  resource_group_name = var.resource_group_name
  location            = var.secondary_location
  capacity            = var.capacity
  family              = var.family
  sku_name            = var.sku_name
  enable_non_ssl_port = false
  minimum_tls_version = "1.2"

  redis_configuration {
    maxmemory_policy = "allkeys-lru"
  }

  tags = var.tags
}

resource "azurerm_redis_linked_server" "geo" {
  count                       = var.enable_geo_replication ? 1 : 0
  target_redis_cache_name     = azurerm_redis_cache.this.name
  resource_group_name         = var.resource_group_name
  linked_redis_cache_id       = azurerm_redis_cache.secondary[0].id
  linked_redis_cache_location = var.secondary_location
  server_role                 = "Secondary"
}
