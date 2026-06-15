terraform {
  required_providers {
    azurerm = { source = "hashicorp/azurerm"; version = "~> 3.110" }
  }
}

locals {
  prefix       = "rush-order-${var.environment}"
  storage_name = "rushorder${var.environment}sa"  # no hyphens, max 24 chars
}

resource "azurerm_storage_account" "this" {
  name                            = local.storage_name
  resource_group_name             = var.resource_group_name
  location                        = var.location
  account_tier                    = "Standard"
  account_replication_type        = var.account_replication_type
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = true

  blob_properties {
    cors_rule {
      allowed_headers    = ["*"]
      allowed_methods    = ["GET", "HEAD"]
      allowed_origins    = ["*"]
      exposed_headers    = ["*"]
      max_age_in_seconds = 3600
    }
  }

  tags = var.tags
}

# product-images — public blob read (served via CDN)
resource "azurerm_storage_container" "product_images" {
  name                  = "product-images"
  storage_account_name  = azurerm_storage_account.this.name
  container_access_type = "blob"
}

# receipts — private, access via SAS token
resource "azurerm_storage_container" "receipts" {
  name                  = "receipts"
  storage_account_name  = azurerm_storage_account.this.name
  container_access_type = "private"
}

# backups — private
resource "azurerm_storage_container" "backups" {
  name                  = "backups"
  storage_account_name  = azurerm_storage_account.this.name
  container_access_type = "private"
}

# CDN profile pointing at product-images
resource "azurerm_cdn_profile" "this" {
  name                = "${local.prefix}-cdn"
  resource_group_name = var.resource_group_name
  location            = "global"
  sku                 = "Standard_Microsoft"
  tags                = var.tags
}

resource "azurerm_cdn_endpoint" "product_images" {
  name                = "${local.prefix}-images"
  profile_name        = azurerm_cdn_profile.this.name
  resource_group_name = var.resource_group_name
  location            = "global"
  origin_host_header  = azurerm_storage_account.this.primary_blob_host
  origin_path         = "/product-images"

  origin {
    name      = "storage-origin"
    host_name = azurerm_storage_account.this.primary_blob_host
  }

  delivery_rule {
    name  = "EnforceHTTPS"
    order = 1

    request_scheme_condition {
      operator     = "Equal"
      match_values = ["HTTP"]
    }

    url_redirect_action {
      redirect_type = "Found"
      protocol      = "Https"
    }
  }

  tags = var.tags
}
