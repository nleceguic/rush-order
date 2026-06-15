terraform {
  required_providers {
    azurerm = { source = "hashicorp/azurerm"; version = "~> 3.110" }
  }
}

locals {
  prefix = "rush-order-${var.environment}"

  queues = toset([
    "orders.new",
    "orders.status-changed",
    "notifications.email",
    "notifications.push",
  ])
}

resource "azurerm_servicebus_namespace" "this" {
  name                = "${local.prefix}-sb"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = var.sku
  tags                = var.tags
}

resource "azurerm_servicebus_queue" "this" {
  for_each     = local.queues
  name         = each.key
  namespace_id = azurerm_servicebus_namespace.this.id

  dead_lettering_on_message_expiration = true
  max_delivery_count                   = 10
  lock_duration                        = "PT5M"
  default_message_ttl                  = "P14D"
  requires_session                     = false
}

# Topic: restaurant-events (fan-out per restaurant)
resource "azurerm_servicebus_topic" "restaurant_events" {
  name         = "restaurant-events"
  namespace_id = azurerm_servicebus_namespace.this.id

  default_message_ttl = "P14D"
}

# Default catch-all subscription; per-restaurant filters added by the app
resource "azurerm_servicebus_subscription" "all_restaurants" {
  name                                 = "all-restaurants"
  topic_id                             = azurerm_servicebus_topic.restaurant_events.id
  max_delivery_count                   = 10
  lock_duration                        = "PT5M"
  default_message_ttl                  = "P14D"
  dead_lettering_on_message_expiration = true
}
