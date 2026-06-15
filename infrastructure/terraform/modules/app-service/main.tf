terraform {
  required_providers {
    azurerm = { source = "hashicorp/azurerm"; version = "~> 3.110" }
  }
}

locals {
  prefix = "rush-order-${var.environment}"
  aspnetcore_env = {
    dev     = "Development"
    staging = "Staging"
    prod    = "Production"
  }
  kv_ref = "VaultName=${var.key_vault_name};SecretName"
}

# ── App Service Plan ──────────────────────────────────────────────────────────
resource "azurerm_service_plan" "this" {
  name                = "${local.prefix}-asp"
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Linux"
  sku_name            = var.app_service_plan_sku
  tags                = var.tags
}

# ── App Service (Linux + Docker) ──────────────────────────────────────────────
resource "azurerm_linux_web_app" "this" {
  name                = "${local.prefix}-app"
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.this.id
  https_only          = true

  identity { type = "SystemAssigned" }

  site_config {
    always_on           = var.app_service_plan_sku != "B1"
    health_check_path   = "/health"
    minimum_tls_version = "1.2"

    application_stack {
      docker_image_name        = var.docker_image
      docker_registry_url      = var.docker_registry_url
      docker_registry_username = var.docker_registry_username
    }
  }

  app_settings = {
    APPLICATIONINSIGHTS_CONNECTION_STRING           = var.application_insights_connection_string
    ApplicationInsightsAgent_EXTENSION_VERSION       = "~3"
    ASPNETCORE_ENVIRONMENT                           = lookup(local.aspnetcore_env, var.environment, "Development")
    WEBSITES_ENABLE_APP_SERVICE_STORAGE              = "false"

    DOCKER_REGISTRY_SERVER_URL                       = var.docker_registry_url
    DOCKER_REGISTRY_SERVER_USERNAME                  = var.docker_registry_username
    DOCKER_REGISTRY_SERVER_PASSWORD                  = "@Microsoft.KeyVault(${local.kv_ref}=DockerRegistryPassword)"

    "ConnectionStrings__DefaultConnection"           = "@Microsoft.KeyVault(${local.kv_ref}=ConnectionStringsDefaultConnection)"
    "ConnectionStrings__Redis"                       = "@Microsoft.KeyVault(${local.kv_ref}=RedisConnectionString)"
    "ConnectionStrings__ServiceBus"                  = "@Microsoft.KeyVault(${local.kv_ref}=ServiceBusConnectionString)"
    "JwtSettings__Secret"                            = "@Microsoft.KeyVault(${local.kv_ref}=JwtSecret)"
    "Stripe__SecretKey"                              = "@Microsoft.KeyVault(${local.kv_ref}=StripeKey)"
    "SendGrid__ApiKey"                               = "@Microsoft.KeyVault(${local.kv_ref}=SendGridKey)"
    "Vapid__PublicKey"                               = "@Microsoft.KeyVault(${local.kv_ref}=VapidPublicKey)"
    "Vapid__PrivateKey"                              = "@Microsoft.KeyVault(${local.kv_ref}=VapidPrivateKey)"
  }

  tags = var.tags

  lifecycle {
    ignore_changes = [app_settings["WEBSITE_SWAP_WARMUP_PING_PATH"]]
  }
}

# ── VNet Integration ──────────────────────────────────────────────────────────
resource "azurerm_app_service_virtual_network_swift_connection" "this" {
  app_service_id = azurerm_linux_web_app.this.id
  subnet_id      = var.app_subnet_id
}

# ── Key Vault RBAC ────────────────────────────────────────────────────────────
resource "azurerm_role_assignment" "kv_secrets_user" {
  scope                = var.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.this.identity[0].principal_id
}

# ── Autoscaling (prod: min 2 / max 10 / CPU > 70%) ───────────────────────────
resource "azurerm_monitor_autoscale_setting" "this" {
  count               = var.enable_autoscaling ? 1 : 0
  name                = "${local.prefix}-autoscale"
  resource_group_name = var.resource_group_name
  location            = var.location
  target_resource_id  = azurerm_service_plan.this.id
  tags                = var.tags

  profile {
    name = "default"
    capacity { default = 2; minimum = 2; maximum = 10 }

    rule {
      metric_trigger {
        metric_name        = "CpuPercentage"
        metric_resource_id = azurerm_service_plan.this.id
        time_grain         = "PT1M"
        statistic          = "Average"
        time_window        = "PT5M"
        time_aggregation   = "Average"
        operator           = "GreaterThan"
        threshold          = 70
      }
      scale_action { direction = "Increase"; type = "ChangeCount"; value = "1"; cooldown = "PT5M" }
    }

    rule {
      metric_trigger {
        metric_name        = "CpuPercentage"
        metric_resource_id = azurerm_service_plan.this.id
        time_grain         = "PT1M"
        statistic          = "Average"
        time_window        = "PT10M"
        time_aggregation   = "Average"
        operator           = "LessThan"
        threshold          = 30
      }
      scale_action { direction = "Decrease"; type = "ChangeCount"; value = "1"; cooldown = "PT10M" }
    }
  }
}

# ── Staging deployment slot (blue/green, prod only) ───────────────────────────
resource "azurerm_linux_web_app_slot" "staging" {
  count          = var.enable_deployment_slot ? 1 : 0
  name           = "staging"
  app_service_id = azurerm_linux_web_app.this.id
  https_only     = true

  identity { type = "SystemAssigned" }

  site_config {
    always_on           = true
    health_check_path   = "/health"
    minimum_tls_version = "1.2"

    application_stack {
      docker_image_name        = var.docker_image
      docker_registry_url      = var.docker_registry_url
      docker_registry_username = var.docker_registry_username
    }
  }

  app_settings = {
    APPLICATIONINSIGHTS_CONNECTION_STRING = var.application_insights_connection_string
    ASPNETCORE_ENVIRONMENT                = "Staging"
    WEBSITES_ENABLE_APP_SERVICE_STORAGE   = "false"
  }

  tags = var.tags
}

resource "azurerm_role_assignment" "kv_slot" {
  count                = var.enable_deployment_slot ? 1 : 0
  scope                = var.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app_slot.staging[0].identity[0].principal_id
}
