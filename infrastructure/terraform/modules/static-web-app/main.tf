terraform {
  required_providers {
    azurerm = { source = "hashicorp/azurerm"; version = "~> 3.110" }
  }
}

locals {
  prefix = "rush-order-${var.environment}"
}

# ── Azure Static Web App ──────────────────────────────────────────────────────
resource "azurerm_static_site" "this" {
  name                = "${local.prefix}-swa"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku_tier            = var.sku_tier
  sku_size            = var.sku_tier
  tags                = var.tags
}

# ── Custom domain + Azure-managed SSL certificate (Let's Encrypt) ─────────────
# Validation type: cname-delegation — add a CNAME record at your DNS provider:
#   app.rushorder.es  CNAME  <default_host_name>
# Azure issues and renews the certificate automatically.
resource "azurerm_static_site_custom_domain" "this" {
  count          = var.custom_hostname != "" ? 1 : 0
  static_site_id = azurerm_static_site.this.id
  domain_name    = var.custom_hostname
  # cname-delegation: DNS CNAME must point to azurerm_static_site.this.default_host_name
  validation_type = "cname-delegation"
}
