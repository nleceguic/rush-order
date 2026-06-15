output "namespace_id"   { value = azurerm_servicebus_namespace.this.id }
output "namespace_name" { value = azurerm_servicebus_namespace.this.name }

output "primary_connection_string" {
  sensitive = true
  value     = azurerm_servicebus_namespace.this.default_primary_connection_string
}

output "queue_ids" {
  value = { for k, v in azurerm_servicebus_queue.this : k => v.id }
}

output "restaurant_events_topic_id" {
  value = azurerm_servicebus_topic.restaurant_events.id
}
