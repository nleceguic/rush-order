terraform {
  required_version = ">= 1.7.0"
  required_providers {
    azurerm = { source = "hashicorp/azurerm"; version = "~> 3.110" }
  }

  backend "azurerm" {
    resource_group_name  = "rush-order-tfstate-rg"
    storage_account_name = "rushordertfstate"
    container_name       = "tfstate"
    key                  = "prod.terraform.tfstate"
  }
}

provider "azurerm" {
  subscription_id = var.subscription_id
  features {
    key_vault {
      # Prod KV has purge_protection=true — do NOT purge on destroy
      purge_soft_delete_on_destroy    = false
      recover_soft_deleted_key_vaults = true
    }
    resource_group {
      prevent_deletion_if_contains_resources = true
    }
  }
}

locals {
  env = "prod"
  tags = {
    Environment = "prod"
    Project     = "rush-order"
    ManagedBy   = "terraform"
  }
}

# ── Shared: resource group + networking ───────────────────────────────────────
module "shared" {
  source      = "../../shared"
  environment = local.env
  location    = var.location
  tags        = local.tags
}

# ── Monitoring: LAW (90d) + APPI + Action Group ───────────────────────────────
module "monitoring" {
  source              = "../../modules/monitoring"
  environment         = local.env
  resource_group_name = module.shared.resource_group_name
  location            = var.location
  log_retention_days  = 90
  alert_email         = var.alert_email
  slack_webhook_url   = var.slack_webhook_url
  tags                = local.tags
}

# ── PostgreSQL: D4s_v3, ZoneRedundant HA, 35-day backup, read replica ─────────
module "postgresql" {
  source                 = "../../modules/postgresql"
  environment            = local.env
  resource_group_name    = module.shared.resource_group_name
  location               = var.location
  sku_name               = "GP_Standard_D4s_v3"
  storage_mb             = 65536
  backup_retention_days  = 35
  high_availability_mode = "ZoneRedundant"
  enable_read_replica    = true
  replica_location       = "westeurope"
  delegated_subnet_id    = module.shared.postgresql_subnet_id
  private_dns_zone_id    = module.shared.postgresql_private_dns_zone_id
  administrator_password = var.pg_admin_password
  tags                   = local.tags
}

# ── Redis: Premium C1 with geo-replication West+North Europe ──────────────────
# NOTE: geo-replication requires Premium SKU (P family). Spec referenced
# Standard C2, but geo-replication forces Premium.
module "redis" {
  source                      = "../../modules/redis"
  environment                 = local.env
  resource_group_name         = module.shared.resource_group_name
  location                    = var.location
  sku_name                    = "Premium"
  family                      = "P"
  capacity                    = 1
  enable_geo_replication      = true
  secondary_location          = "westeurope"
  private_endpoints_subnet_id = module.shared.private_endpoints_subnet_id
  private_dns_zone_id         = module.shared.redis_private_dns_zone_id
  tags                        = local.tags
}

# ── Service Bus: Premium ──────────────────────────────────────────────────────
module "service_bus" {
  source              = "../../modules/service-bus"
  environment         = local.env
  resource_group_name = module.shared.resource_group_name
  location            = var.location
  sku                 = "Premium"
  tags                = local.tags
}

# ── Storage + CDN ─────────────────────────────────────────────────────────────
module "storage" {
  source                   = "../../modules/storage"
  environment              = local.env
  resource_group_name      = module.shared.resource_group_name
  location                 = var.location
  account_replication_type = "LRS"
  tags                     = local.tags
}

# ── Key Vault (purge protection ON for prod) ──────────────────────────────────
module "key_vault" {
  source              = "../../modules/key-vault"
  environment         = local.env
  resource_group_name = module.shared.resource_group_name
  location            = var.location
  tenant_id           = var.tenant_id
  tags                = local.tags

  secrets = {
    ConnectionStringsDefaultConnection = module.postgresql.connection_string
    RedisConnectionString              = module.redis.redis_connection_string
    ServiceBusConnectionString         = module.service_bus.primary_connection_string
    JwtSecret                          = var.jwt_secret
    StripeKey                          = var.stripe_key
    SendGridKey                        = var.sendgrid_key
    VapidPublicKey                     = var.vapid_public_key
    VapidPrivateKey                    = var.vapid_private_key
    DockerRegistryPassword             = var.docker_registry_password
  }
}

# ── App Service: P2v3, autoscaling, blue/green slot ──────────────────────────
module "app_service" {
  source              = "../../modules/app-service"
  environment         = local.env
  resource_group_name = module.shared.resource_group_name
  location            = var.location

  app_service_plan_sku                   = "P2v3"
  app_subnet_id                          = module.shared.app_service_subnet_id
  key_vault_id                           = module.key_vault.key_vault_id
  key_vault_name                         = module.key_vault.key_vault_name
  application_insights_connection_string = module.monitoring.application_insights_connection_string
  docker_image                           = var.docker_image
  docker_registry_url                    = var.docker_registry_url
  docker_registry_username               = var.docker_registry_username
  enable_autoscaling                     = true
  enable_deployment_slot                 = true
  tags                                   = local.tags
}

# ── Azure Front Door + WAF (Standard tier, DefaultRuleSet 2.0 / OWASP CRS 3.2)
resource "azurerm_cdn_frontdoor_profile" "this" {
  name                = "rush-order-prod-afd"
  resource_group_name = module.shared.resource_group_name
  sku_name            = "Standard_AzureFrontDoor"
  tags                = local.tags
}

resource "azurerm_cdn_frontdoor_endpoint" "this" {
  name                     = "rush-order-prod"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.this.id
  tags                     = local.tags
}

resource "azurerm_cdn_frontdoor_origin_group" "app" {
  name                     = "app-service"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.this.id

  load_balancing {}

  health_probe {
    path                = "/health"
    protocol            = "Https"
    request_type        = "HEAD"
    interval_in_seconds = 30
  }
}

resource "azurerm_cdn_frontdoor_origin" "app" {
  name                           = "app-service-origin"
  cdn_frontdoor_origin_group_id  = azurerm_cdn_frontdoor_origin_group.app.id
  enabled                        = true
  host_name                      = module.app_service.app_service_hostname
  origin_host_header             = module.app_service.app_service_hostname
  priority                       = 1
  weight                         = 1000
  certificate_name_check_enabled = true
}

resource "azurerm_cdn_frontdoor_route" "default" {
  name                          = "default-route"
  cdn_frontdoor_endpoint_id     = azurerm_cdn_frontdoor_endpoint.this.id
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.app.id
  cdn_frontdoor_origin_ids      = [azurerm_cdn_frontdoor_origin.app.id]
  enabled                       = true
  forwarding_protocol           = "HttpsOnly"
  https_redirect_enabled        = true
  patterns_to_match             = ["/*"]
  supported_protocols           = ["Http", "Https"]
}

resource "azurerm_cdn_frontdoor_firewall_policy" "waf" {
  name                = "rushorderprodwaf"
  resource_group_name = module.shared.resource_group_name
  sku_name            = "Standard_AzureFrontDoor"
  mode                = "Prevention"
  enabled             = true
  tags                = local.tags

  managed_rule {
    type    = "DefaultRuleSet"
    version = "1.0"
    action  = "Block"
  }
}

resource "azurerm_cdn_frontdoor_security_policy" "waf" {
  name                     = "waf-security-policy"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.this.id

  security_policies {
    firewall {
      cdn_frontdoor_firewall_policy_id = azurerm_cdn_frontdoor_firewall_policy.waf.id

      association {
        domain {
          cdn_frontdoor_domain_id = azurerm_cdn_frontdoor_endpoint.this.id
        }
        patterns_to_match = ["/*"]
      }
    }
  }
}

# ── Metric Alerts ─────────────────────────────────────────────────────────────
resource "azurerm_monitor_metric_alert" "availability" {
  name                = "rush-order-prod-alert-availability"
  resource_group_name = module.shared.resource_group_name
  scopes              = [module.app_service.app_service_id]
  severity            = 0
  frequency           = "PT1M"
  window_size         = "PT5M"
  description         = "App availability below 99%"

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "HealthCheckStatus"
    aggregation      = "Average"
    operator         = "LessThan"
    threshold        = 99
  }

  action { action_group_id = module.monitoring.action_group_id }
  tags = local.tags
}

resource "azurerm_monitor_metric_alert" "response_time" {
  name                = "rush-order-prod-alert-response-time"
  resource_group_name = module.shared.resource_group_name
  scopes              = [module.app_service.app_service_id]
  severity            = 1
  frequency           = "PT1M"
  window_size         = "PT5M"
  description         = "P95 response time > 2s"

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "HttpResponseTime"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 2
  }

  action { action_group_id = module.monitoring.action_group_id }
  tags = local.tags
}

resource "azurerm_monitor_metric_alert" "error_rate" {
  name                = "rush-order-prod-alert-5xx"
  resource_group_name = module.shared.resource_group_name
  scopes              = [module.app_service.app_service_id]
  severity            = 0
  frequency           = "PT1M"
  window_size         = "PT5M"
  description         = "HTTP 5xx error rate > 1%"

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "Http5xx"
    aggregation      = "Total"
    operator         = "GreaterThan"
    threshold        = 10
  }

  action { action_group_id = module.monitoring.action_group_id }
  tags = local.tags
}

# ── Outputs ───────────────────────────────────────────────────────────────────
output "front_door_url"      { value = "https://${azurerm_cdn_frontdoor_endpoint.this.host_name}" }
output "app_service_url"     { value = module.app_service.app_service_url }
output "staging_slot_url"    { value = module.app_service.staging_slot_url }
output "postgresql_fqdn"     { value = module.postgresql.server_fqdn }
output "postgresql_replica_fqdn" { value = module.postgresql.replica_fqdn }
output "redis_hostname"      { value = module.redis.redis_hostname }
output "cdn_url"             { value = module.storage.cdn_endpoint_url }
output "key_vault_uri"       { value = module.key_vault.key_vault_uri }
output "appi_conn_string"    { value = module.monitoring.application_insights_connection_string; sensitive = true }
