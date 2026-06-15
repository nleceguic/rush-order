terraform {
  required_providers {
    azurerm = { source = "hashicorp/azurerm"; version = "~> 3.110" }
  }
}

locals {
  prefix = "rush-order-${var.environment}"
}

# Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "this" {
  name                = "${local.prefix}-law"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "PerGB2018"
  retention_in_days   = var.log_retention_days
  tags                = var.tags
}

# Application Insights (workspace-based)
resource "azurerm_application_insights" "this" {
  name                = "${local.prefix}-appi"
  resource_group_name = var.resource_group_name
  location            = var.location
  workspace_id        = azurerm_log_analytics_workspace.this.id
  application_type    = "web"
  tags                = var.tags
}

# Action Group — email + Slack webhook
resource "azurerm_monitor_action_group" "critical" {
  name                = "${local.prefix}-ag-critical"
  resource_group_name = var.resource_group_name
  short_name          = "ro-${var.environment}"
  tags                = var.tags

  email_receiver {
    name                    = "ops-team"
    email_address           = var.alert_email
    use_common_alert_schema = true
  }

  dynamic "webhook_receiver" {
    for_each = var.slack_webhook_url != "" ? [1] : []
    content {
      name                    = "slack"
      service_uri             = var.slack_webhook_url
      use_common_alert_schema = true
    }
  }
}
