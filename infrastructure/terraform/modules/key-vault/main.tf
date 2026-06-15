terraform {
  required_providers {
    azurerm = { source = "hashicorp/azurerm"; version = "~> 3.110" }
  }
}

data "azurerm_client_config" "current" {}

locals {
  prefix = "rush-order-${var.environment}"
}

resource "azurerm_key_vault" "this" {
  name                       = "${local.prefix}-kv"
  resource_group_name        = var.resource_group_name
  location                   = var.location
  tenant_id                  = var.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7

  # Purge protection locked ON for prod — cannot be disabled once set
  purge_protection_enabled  = var.environment == "prod"
  enable_rbac_authorization = true

  network_acls {
    default_action = "Allow"
    bypass         = "AzureServices"
  }

  tags = var.tags
}

# Grant the Terraform principal (CI service principal / developer) rights to write secrets
resource "azurerm_role_assignment" "terraform_admin" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Populate secrets — depend on the role assignment so write access exists
resource "azurerm_key_vault_secret" "this" {
  for_each     = var.secrets
  name         = each.key
  value        = each.value
  key_vault_id = azurerm_key_vault.this.id

  depends_on = [azurerm_role_assignment.terraform_admin]
}
