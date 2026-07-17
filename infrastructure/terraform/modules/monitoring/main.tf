terraform {
  required_providers {
    azurerm = { source = "hashicorp/azurerm"; version = "~> 3.110" }
  }
}

locals {
  prefix       = "rush-order-${var.environment}"
  warning_mail = var.warning_alert_email != "" ? var.warning_alert_email : var.alert_email
}

# ── Log Analytics Workspace ───────────────────────────────────────────────────
resource "azurerm_log_analytics_workspace" "this" {
  name                = "${local.prefix}-law"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "PerGB2018"
  retention_in_days   = var.log_retention_days
  tags                = var.tags
}

# Cold-tier archive (prod only, compliance 2-year requirement)
resource "azurerm_log_analytics_workspace_table" "archive" {
  for_each      = var.log_archive_days > 0 ? toset(["AppTraces", "AppExceptions", "AppRequests"]) : toset([])
  workspace_id  = azurerm_log_analytics_workspace.this.id
  name          = each.key
  # total_retention_in_days = hot + archive (e.g. 90 + 730 = 820)
  total_retention_in_days = var.log_retention_days + var.log_archive_days
  plan                    = "Analytics"
}

# ── Application Insights (workspace-based) ────────────────────────────────────
resource "azurerm_application_insights" "this" {
  name                = "${local.prefix}-appi"
  resource_group_name = var.resource_group_name
  location            = var.location
  workspace_id        = azurerm_log_analytics_workspace.this.id
  application_type    = "web"
  tags                = var.tags
}

# ── Action Groups ─────────────────────────────────────────────────────────────

# Critical — email + Slack pager (immediate)
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

# Warning — email only (no pager)
resource "azurerm_monitor_action_group" "warning" {
  name                = "${local.prefix}-ag-warning"
  resource_group_name = var.resource_group_name
  short_name          = "ro-warn"
  tags                = var.tags

  email_receiver {
    name                    = "ops-team-warn"
    email_address           = local.warning_mail
    use_common_alert_schema = true
  }
}

# ── Azure Workbook — Rush Order Operations Dashboard ──────────────────────────
resource "azurerm_application_insights_workbook" "dashboard" {
  name                = "${local.prefix}-workbook"
  resource_group_name = var.resource_group_name
  location            = var.location
  display_name        = "Rush Order — Operations Dashboard (${var.environment})"
  tags                = var.tags

  # Workbook template — panels: req/s, error rate, latency, orders/h, SignalR, cache
  data_json = jsonencode({
    version = "Notebook/1.0"
    items = [
      # ── Requests per second ──────────────────────────────────────────────
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-KQL
            requests
            | where timestamp > ago(1h)
            | summarize rps = count() / 60.0 by bin(timestamp, 1m)
            | render timechart
          KQL
          size            = 0
          title           = "Requests / second"
          timeContext     = { durationMs = 3600000 }
          queryType       = 0
          resourceType    = "microsoft.insights/components"
          crossComponentResources = [azurerm_application_insights.this.id]
        }
        name = "rps"
      },
      # ── Error rate by endpoint ───────────────────────────────────────────
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-KQL
            requests
            | where timestamp > ago(1h) and success == false
            | summarize errors = count() by name
            | order by errors desc
            | take 10
            | render barchart
          KQL
          size            = 1
          title           = "Error rate by endpoint (last 1h)"
          queryType       = 0
          resourceType    = "microsoft.insights/components"
          crossComponentResources = [azurerm_application_insights.this.id]
        }
        name = "errors"
      },
      # ── Latency P50 / P95 / P99 ─────────────────────────────────────────
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-KQL
            requests
            | where timestamp > ago(1h)
            | summarize
                p50 = percentile(duration, 50),
                p95 = percentile(duration, 95),
                p99 = percentile(duration, 99)
              by bin(timestamp, 5m)
            | render timechart
          KQL
          size            = 0
          title           = "Latency — P50 / P95 / P99 (ms)"
          queryType       = 0
          resourceType    = "microsoft.insights/components"
          crossComponentResources = [azurerm_application_insights.this.id]
        }
        name = "latency"
      },
      # ── Orders created per hour ──────────────────────────────────────────
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-KQL
            customMetrics
            | where timestamp > ago(24h) and name == "orders.placed"
            | summarize orders = sum(value) by bin(timestamp, 1h)
            | render columnchart
          KQL
          size            = 1
          title           = "Orders created per hour (last 24h)"
          queryType       = 0
          resourceType    = "microsoft.insights/components"
          crossComponentResources = [azurerm_application_insights.this.id]
        }
        name = "orders"
      },
      # ── Active SignalR connections ────────────────────────────────────────
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-KQL
            customMetrics
            | where timestamp > ago(1h) and name == "signalr.connections.active"
            | summarize active = avg(value) by bin(timestamp, 1m), tostring(customDimensions["restaurant_id"])
            | render timechart
          KQL
          size            = 0
          title           = "Active SignalR connections per restaurant"
          queryType       = 0
          resourceType    = "microsoft.insights/components"
          crossComponentResources = [azurerm_application_insights.this.id]
        }
        name = "signalr"
      },
      # ── Cache hit rate ────────────────────────────────────────────────────
      {
        type = 3
        content = {
          version = "KqlItem/1.0"
          query   = <<-KQL
            customMetrics
            | where timestamp > ago(1h) and name == "menu.cache.hit_rate"
            | summarize hit_rate = avg(value) by bin(timestamp, 5m)
            | render timechart
          KQL
          size            = 0
          title           = "Menu cache hit rate"
          queryType       = 0
          resourceType    = "microsoft.insights/components"
          crossComponentResources = [azurerm_application_insights.this.id]
        }
        name = "cache"
      }
    ]
  })
}
