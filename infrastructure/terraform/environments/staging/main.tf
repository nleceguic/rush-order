terraform {
  required_version = ">= 1.7.0"
  required_providers {
    azurerm = { source = "hashicorp/azurerm"; version = "~> 3.110" }
  }

  backend "azurerm" {
    resource_group_name  = "rush-order-tfstate-rg"
    storage_account_name = "rushordertfstate"
    container_name       = "tfstate"
    key                  = "staging.terraform.tfstate"
  }
}

provider "azurerm" {
  subscription_id = var.subscription_id
  features {
    key_vault {
      purge_soft_delete_on_destroy    = true
      recover_soft_deleted_key_vaults = true
    }
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
  }
}

locals {
  env = "staging"
  tags = {
    Environment = "staging"
    Project     = "rush-order"
    ManagedBy   = "terraform"
  }
}

module "shared" {
  source      = "../../shared"
  environment = local.env
  location    = var.location
  tags        = local.tags
}

module "monitoring" {
  source              = "../../modules/monitoring"
  environment         = local.env
  resource_group_name = module.shared.resource_group_name
  location            = var.location
  log_retention_days  = 30
  alert_email         = var.alert_email
  slack_webhook_url   = var.slack_webhook_url
  tags                = local.tags
}

module "postgresql" {
  source                 = "../../modules/postgresql"
  environment            = local.env
  resource_group_name    = module.shared.resource_group_name
  location               = var.location
  sku_name               = "GP_Standard_D2s_v3"
  backup_retention_days  = 7
  high_availability_mode = "Disabled"
  enable_read_replica    = false
  delegated_subnet_id    = module.shared.postgresql_subnet_id
  private_dns_zone_id    = module.shared.postgresql_private_dns_zone_id
  administrator_password = var.pg_admin_password
  tags                   = local.tags
}

module "redis" {
  source                      = "../../modules/redis"
  environment                 = local.env
  resource_group_name         = module.shared.resource_group_name
  location                    = var.location
  sku_name                    = "Standard"
  family                      = "C"
  capacity                    = 1
  enable_geo_replication      = false
  private_endpoints_subnet_id = module.shared.private_endpoints_subnet_id
  private_dns_zone_id         = module.shared.redis_private_dns_zone_id
  tags                        = local.tags
}

module "service_bus" {
  source              = "../../modules/service-bus"
  environment         = local.env
  resource_group_name = module.shared.resource_group_name
  location            = var.location
  sku                 = "Standard"
  tags                = local.tags
}

module "storage" {
  source                   = "../../modules/storage"
  environment              = local.env
  resource_group_name      = module.shared.resource_group_name
  location                 = var.location
  account_replication_type = "LRS"
  tags                     = local.tags
}

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

module "app_service" {
  source              = "../../modules/app-service"
  environment         = local.env
  resource_group_name = module.shared.resource_group_name
  location            = var.location

  app_service_plan_sku                   = "P1v3"
  app_subnet_id                          = module.shared.app_service_subnet_id
  key_vault_id                           = module.key_vault.key_vault_id
  key_vault_name                         = module.key_vault.key_vault_name
  application_insights_connection_string = module.monitoring.application_insights_connection_string
  docker_image                           = var.docker_image
  docker_registry_url                    = var.docker_registry_url
  docker_registry_username               = var.docker_registry_username
  enable_autoscaling                     = false
  enable_deployment_slot                 = false
  tags                                   = local.tags
}

resource "azurerm_monitor_metric_alert" "availability" {
  name                = "rush-order-staging-alert-availability"
  resource_group_name = module.shared.resource_group_name
  scopes              = [module.app_service.app_service_id]
  severity            = 1; frequency = "PT5M"; window_size = "PT15M"
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
  name                = "rush-order-staging-alert-response-time"
  resource_group_name = module.shared.resource_group_name
  scopes              = [module.app_service.app_service_id]
  severity            = 2; frequency = "PT5M"; window_size = "PT15M"
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
  name                = "rush-order-staging-alert-5xx"
  resource_group_name = module.shared.resource_group_name
  scopes              = [module.app_service.app_service_id]
  severity            = 1; frequency = "PT5M"; window_size = "PT15M"
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

# ── Azure Static Web Apps — PWA (preview envs for each PR, auto SSL) ──────────
module "static_web_app" {
  source              = "../../modules/static-web-app"
  environment         = local.env
  resource_group_name = module.shared.resource_group_name
  location            = "westeurope"   # SWA has limited region availability
  sku_tier            = "Standard"     # Standard: preview environments per PR
  custom_hostname     = ""             # staging uses the default azurestaticapps.net URL
  tags                = local.tags
}

output "app_service_url"  { value = module.app_service.app_service_url }
output "pwa_url"          { value = module.static_web_app.app_url }
output "postgresql_fqdn"  { value = module.postgresql.server_fqdn }
output "redis_hostname"   { value = module.redis.redis_hostname }
output "cdn_url"          { value = module.storage.cdn_endpoint_url }
output "key_vault_uri"    { value = module.key_vault.key_vault_uri }

output "swa_api_key" {
  sensitive   = true
  description = "Store as GitHub secret AZURE_STATIC_WEB_APPS_API_TOKEN_STAGING"
  value       = module.static_web_app.api_key
}
