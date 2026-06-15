terraform {
  required_providers {
    azurerm = { source = "hashicorp/azurerm"; version = "~> 3.110" }
  }
}

locals {
  prefix = "rush-order-${var.environment}"
}

resource "azurerm_postgresql_flexible_server" "this" {
  name                   = "${local.prefix}-psql"
  resource_group_name    = var.resource_group_name
  location               = var.location
  version                = var.postgres_version
  delegated_subnet_id    = var.delegated_subnet_id
  private_dns_zone_id    = var.private_dns_zone_id
  administrator_login    = var.administrator_login
  administrator_password = var.administrator_password
  sku_name               = var.sku_name
  storage_mb             = var.storage_mb
  backup_retention_days  = var.backup_retention_days
  geo_redundant_backup_enabled = var.environment == "prod"

  dynamic "high_availability" {
    for_each = var.high_availability_mode != "Disabled" ? [1] : []
    content {
      mode = var.high_availability_mode
    }
  }

  maintenance_window {
    day_of_week  = 0
    start_hour   = 2
    start_minute = 0
  }

  tags = var.tags
}

resource "azurerm_postgresql_flexible_server_database" "rushorder" {
  name      = "rushorder"
  server_id = azurerm_postgresql_flexible_server.this.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

# Read replica in secondary region (prod only)
resource "azurerm_postgresql_flexible_server" "replica" {
  count               = var.enable_read_replica ? 1 : 0
  name                = "${local.prefix}-psql-replica"
  resource_group_name = var.resource_group_name
  location            = var.replica_location
  create_mode         = "Replica"
  source_server_id    = azurerm_postgresql_flexible_server.this.id
  sku_name            = var.sku_name
  storage_mb          = var.storage_mb
  version             = var.postgres_version
  tags                = var.tags
}
