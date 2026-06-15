output "application_insights_id" { value = azurerm_application_insights.this.id }

output "application_insights_connection_string" {
  sensitive = true
  value     = azurerm_application_insights.this.connection_string
}

output "instrumentation_key" {
  sensitive = true
  value     = azurerm_application_insights.this.instrumentation_key
}

output "log_analytics_workspace_id" { value = azurerm_log_analytics_workspace.this.id }
output "action_group_id"            { value = azurerm_monitor_action_group.critical.id }
